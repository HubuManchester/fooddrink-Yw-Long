using System.Text.Json;
using WhatToEat.Models;

#if ANDROID
using Android.Graphics;
#endif

namespace WhatToEat.Services
{
    public class MealService
    {
        private readonly HttpClient _httpClient;

        private const string ApiKey = "ead29520d8de4e76be4980a6d62d540f";
        private const string BaseUrl = "https://api.spoonacular.com/recipes";
        private const string LogMealToken = "3934d71ab5665dfcf8a3cbdf34d2fef19e72969a";
        private const int MaxBytes = 900_000; 

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MealService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        // ════════════════════════════════════════════════════════════════
        // FOOD IMAGE RECOGNITION — LogMeal API
        // ════════════════════════════════════════════════════════════════

        public async Task<FoodImageResult?> ClassifyFoodImageAsync(Stream imageStream, string fileName)
        {
            try
            {
                // 1. 读取原始字节
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                var originalBytes = ms.ToArray();
                Console.WriteLine($"[LogMeal] Original size: {originalBytes.Length} bytes");

                // 2. 压缩到 900KB 以内
                var compressed = CompressImage(originalBytes);
                Console.WriteLine($"[LogMeal] Compressed size: {compressed.Length} bytes");

                // 3. 上传
                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(compressed);
                imageContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "image", "food.jpg");

                using var request1 = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.logmeal.com/v2/image/segmentation/complete")
                {
                    Content = content
                };
                request1.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", LogMealToken);

                using var resp1 = await _httpClient.SendAsync(request1);
                var json1 = await resp1.Content.ReadAsStringAsync();

                Console.WriteLine($"[LogMeal] HTTP {(int)resp1.StatusCode}");
                Console.WriteLine($"[LogMeal] Body: {json1}");

                if (!resp1.IsSuccessStatusCode)
                    return null;

                // 4. 解析识别结果
                long imageId = 0;
                string foodName = string.Empty;
                double probability = 0;

                using (var doc1 = JsonDocument.Parse(json1))
                {
                    var root1 = doc1.RootElement;

                    if (root1.TryGetProperty("imageId", out var idEl))
                        imageId = idEl.GetInt64();
                    else if (root1.TryGetProperty("image_id", out var idEl2))
                        imageId = idEl2.GetInt64();

                    if (root1.TryGetProperty("segmentation_results", out var segs))
                    {
                        foreach (var seg in segs.EnumerateArray())
                        {
                            if (seg.TryGetProperty("recognition_results", out var recs))
                            {
                                foreach (var rec in recs.EnumerateArray())
                                {
                                    foodName = rec.GetProperty("name").GetString() ?? string.Empty;
                                    probability = rec.GetProperty("prob").GetDouble();
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(foodName)) break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(foodName) || imageId == 0)
                {
                    Console.WriteLine("[LogMeal] No food recognised.");
                    return null;
                }

                Console.WriteLine($"[LogMeal] Recognised: {foodName} ({probability:P0})");

                // 5. 查卡路里
                double calories = 0;
                try
                {
                    using var request2 = new HttpRequestMessage(
                        HttpMethod.Post,
                        "https://api.logmeal.com/v2/nutrition/recipe/nutritionalInfo")
                    {
                        Content = new StringContent(
                            $"{{\"imageId\":{imageId}}}",
                            System.Text.Encoding.UTF8,
                            "application/json")
                    };
                    request2.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", LogMealToken);

                    using var resp2 = await _httpClient.SendAsync(request2);
                    if (resp2.IsSuccessStatusCode)
                    {
                        var json2 = await resp2.Content.ReadAsStringAsync();
                        Console.WriteLine($"[LogMeal] Nutrition: {json2}");

                        using var doc2 = JsonDocument.Parse(json2);
                        var root2 = doc2.RootElement;
                        if (root2.TryGetProperty("nutritional_info", out var ni) &&
                            ni.TryGetProperty("calories", out var calEl))
                            calories = calEl.GetDouble();
                        else if (root2.TryGetProperty("calories", out var calEl2))
                            calories = calEl2.GetDouble();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LogMeal] Nutrition skipped: {ex.Message}");
                }

                return new FoodImageResult
                {
                    Category = new FoodCategory { Name = foodName, Probability = probability },
                    Nutrition = new FoodNutrition { Calories = new NutrientValue { Value = calories, Unit = "kcal" } }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] ClassifyFoodImageAsync error: {ex.Message}");
                Console.WriteLine($"[MealService] Stack: {ex.StackTrace}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // 图片压缩
        // Android  → 用 Android.Graphics.Bitmap（最可靠）
        // 其他平台 → 纯字节降质量兜底
        // ════════════════════════════════════════════════════════════════
        private static byte[] CompressImage(byte[] input)
        {
            if (input.Length <= MaxBytes)
                return input;

#if ANDROID
            return CompressAndroid(input);
#else
            // 非 Android 平台：原样返回（模拟器测试用）
            return input;
#endif
        }

#if ANDROID
        private static byte[] CompressAndroid(byte[] input)
        {
            try
            {
                // 解码
                var options = new BitmapFactory.Options { InJustDecodeBounds = false };
                var bitmap  = BitmapFactory.DecodeByteArray(input, 0, input.Length, options);
                if (bitmap == null) return input;

                // 如果尺寸太大，先按比例缩小
                int maxDim = 1024;
                if (bitmap.Width > maxDim || bitmap.Height > maxDim)
                {
                    float scale = (float)maxDim / Math.Max(bitmap.Width, bitmap.Height);
                    int   newW  = (int)(bitmap.Width  * scale);
                    int   newH  = (int)(bitmap.Height * scale);
                    var scaled  = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, newW, newH, true);
                    bitmap.Recycle();
                    bitmap = scaled;
                }

                // 从质量 85 开始往下压，直到低于 900KB
                int[] qualities = { 85, 70, 55, 40, 25 };
                foreach (var q in qualities)
                {
                    using var outStream = new MemoryStream();
                    bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, q, outStream);
                    var result = outStream.ToArray();
                    Console.WriteLine($"[Compress] quality={q} → {result.Length} bytes");
                    if (result.Length <= MaxBytes)
                    {
                        bitmap.Recycle();
                        return result;
                    }
                }

                // 最终兜底：缩到 640px + 质量 20
                float s2   = 640f / Math.Max(bitmap.Width, bitmap.Height);
                var   bmp2 = Android.Graphics.Bitmap.CreateScaledBitmap(
                    bitmap, (int)(bitmap.Width * s2), (int)(bitmap.Height * s2), true);
                bitmap.Recycle();

                using var finalStream = new MemoryStream();
                bmp2.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 20, finalStream);
                bmp2.Recycle();
                return finalStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Compress] Android compress failed: {ex.Message}");
                return input;
            }
        }
#endif

        // ════════════════════════════════════════════════════════════════
        // RECIPE SEARCH — Spoonacular API
        // ════════════════════════════════════════════════════════════════

        public async Task<List<Meal>> SearchMealsByNameAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query)) return new List<Meal>();
                var url = $"{BaseUrl}/complexSearch?apiKey={ApiKey}" +
                          $"&query={Uri.EscapeDataString(query)}&number=10&addRecipeInformation=true&fillIngredients=false";
                var json = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<SpoonacularSearchResponse>(json, JsonOptions)?.Results ?? new List<Meal>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] SearchMealsByName error: {ex.Message}");
                return new List<Meal>();
            }
        }

        public async Task<Meal?> GetRandomMealAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"{BaseUrl}/random?apiKey={ApiKey}&number=1");
                using var doc = JsonDocument.Parse(json);
                var first = doc.RootElement.GetProperty("recipes").EnumerateArray().FirstOrDefault();
                return new Meal
                {
                    Id = first.GetProperty("id").GetInt32(),
                    Title = first.GetProperty("title").GetString() ?? "Unknown",
                    Image = first.GetProperty("image").GetString() ?? string.Empty,
                    ReadyInMinutes = first.GetProperty("readyInMinutes").GetInt32(),
                    Servings = first.GetProperty("servings").GetInt32(),
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] GetRandomMeal error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Meal>> GetMealsByAreaAsync(string area) => await SearchByCuisineAsync(area);

        public async Task<List<Meal>> SearchByCuisineAsync(string cuisine)
        {
            try
            {
                var url = $"{BaseUrl}/complexSearch?apiKey={ApiKey}" +
                          $"&cuisine={Uri.EscapeDataString(cuisine)}&number=10&addRecipeInformation=true";
                var json = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<SpoonacularSearchResponse>(json, JsonOptions)?.Results ?? new List<Meal>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] SearchByCuisine error: {ex.Message}");
                return new List<Meal>();
            }
        }

        public async Task<RecipeDetail?> GetRecipeDetailAsync(int id)
        {
            try
            {
                var url = $"{BaseUrl}/{id}/information?apiKey={ApiKey}&includeNutrition=false";
                var json = await _httpClient.GetStringAsync(url);
                return JsonSerializer.Deserialize<RecipeDetail>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] GetRecipeDetail error: {ex.Message}");
                return null;
            }
        }

        public async Task<RecipeDetail?> SearchAndGetFirstDetailAsync(string query)
        {
            try
            {
                var results = await SearchMealsByNameAsync(query);
                if (results.Count == 0) return null;
                return await GetRecipeDetailAsync(results[0].Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MealService] SearchAndGetFirstDetail error: {ex.Message}");
                return null;
            }
        }

        public static string GetAreaFromCountryCode(string code) => code;
    }
}