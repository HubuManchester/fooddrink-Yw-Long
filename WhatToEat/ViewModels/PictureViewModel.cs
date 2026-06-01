using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    public class PictureViewModel : INotifyPropertyChanged
    {
        private readonly MealService _mealService = new();

        public PictureViewModel()
        {
            TakePhotoCommand = new Command(
                async () => await TakePhotoAsync(),
                () => !IsBusy);

            LogMealCommand = new Command(
                async () => await LogMealAsync(),
                () => HasResult && !IsBusy);

            RetakeCommand = new Command(() =>
            {
                PhotoSource = null;
                FoodName = string.Empty;
                Calories = 0;
                HasResult = false;
                ErrorMessage = string.Empty;
                _locationName = string.Empty;
                LocationDisplay = string.Empty;
                _capturedImagePath = string.Empty;
                StatusText = "Take a photo of your food to identify it";
            }, () => !IsBusy);

            ToggleFlashCommand = new Command(
                async () => await ToggleFlashAsync(),
                () => !IsBusy);

            RefreshCommand = new Command(async () => await ResetAsync());
        }

        // ── Properties ───────────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); RefreshCanExecute(); }
        }
        public bool IsNotBusy => !_isBusy;

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => Set(ref _isRefreshing, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { Set(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        private ImageSource? _photoSource;
        public ImageSource? PhotoSource
        {
            get => _photoSource;
            set { Set(ref _photoSource, value); OnPropertyChanged(nameof(HasPhoto)); }
        }
        public bool HasPhoto => _photoSource != null;

        private string _foodName = string.Empty;
        public string FoodName
        {
            get => _foodName;
            set => Set(ref _foodName, value);
        }

        private int _calories;
        public int Calories
        {
            get => _calories;
            set { Set(ref _calories, value); OnPropertyChanged(nameof(CaloriesLabel)); }
        }
        public string CaloriesLabel => $"{_calories} kcal";

        private bool _hasResult;
        public bool HasResult
        {
            get => _hasResult;
            set { Set(ref _hasResult, value); RefreshCanExecute(); }
        }

        private string _statusText = "Take a photo of your food to identify it";
        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        private string _locationName = string.Empty;
        private string _locationDisplay = string.Empty;
        public string LocationDisplay
        {
            get => _locationDisplay;
            set => Set(ref _locationDisplay, value);
        }

        private bool _isFlashOn;
        public bool IsFlashOn
        {
            get => _isFlashOn;
            set { Set(ref _isFlashOn, value); OnPropertyChanged(nameof(FlashLabel)); }
        }

        public string FlashIcon => _isFlashOn ? "🔦" : "💡";
        public string FlashLabel => _isFlashOn ? "Flash On" : "Flash Off";

        private bool _isFlashSupported = true;
        public bool IsFlashSupported
        {
            get => _isFlashSupported;
            set => Set(ref _isFlashSupported, value);
        }

        private string _capturedImagePath = string.Empty;

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand TakePhotoCommand { get; }
        public ICommand LogMealCommand { get; }
        public ICommand RetakeCommand { get; }
        public ICommand ToggleFlashCommand { get; }
        public ICommand RefreshCommand { get; }

        private async Task ResetAsync()
        {
            if (_isFlashOn)
            {
                try { await Flashlight.Default.TurnOffAsync(); } catch { }
                IsFlashOn = false;
            }

            PhotoSource = null;
            FoodName = string.Empty;
            Calories = 0;
            HasResult = false;
            ErrorMessage = string.Empty;
            _locationName = string.Empty;
            LocationDisplay = string.Empty;
            _capturedImagePath = string.Empty;
            StatusText = "Take a photo of your food to identify it";

            RefreshCanExecute();
            IsRefreshing = false;
        }

        // ── Flash toggle ─────────────────────────────────────────────────

        private async Task ToggleFlashAsync()
        {
            try
            {
                bool supported = await Flashlight.Default.IsSupportedAsync();
                if (!supported)
                {
                    IsFlashSupported = false;
                    ErrorMessage = "Flashlight is not supported on this device.";
                    return;
                }

                if (_isFlashOn)
                {
                    await Flashlight.Default.TurnOffAsync();
                    IsFlashOn = false;
                }
                else
                {
                    await Flashlight.Default.TurnOnAsync();
                    IsFlashOn = true;
                }
            }
            catch (FeatureNotSupportedException)
            {
                IsFlashSupported = false;
                ErrorMessage = "Flashlight is not supported on this device.";
            }
            catch (PermissionException)
            {
                ErrorMessage = "Flashlight permission was denied.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Flash error: {ex.Message}";
                Console.WriteLine($"[PictureViewModel] Flash error: {ex}");
            }
        }

        // ── Camera ───────────────────────────────────────────────────────

        private async Task TakePhotoAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    ErrorMessage = "Camera permission is required to take a photo.";
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync(
                    new MediaPickerOptions { Title = "Take a photo of your food" });

                if (photo == null) return;

                if (_isFlashOn)
                {
                    await Flashlight.Default.TurnOffAsync();
                    IsFlashOn = false;
                }

                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusText = "Identifying your food...";

                _capturedImagePath = photo.FullPath;
                PhotoSource = ImageSource.FromFile(photo.FullPath);

                await using var stream = await photo.OpenReadAsync();
                var recognitionTask = _mealService.ClassifyFoodImageAsync(stream, photo.FileName);
                var locationTask = GetLocationNameAsync();

                await Task.WhenAll(recognitionTask, locationTask);

                var result = await recognitionTask;
                _locationName = await locationTask;
                LocationDisplay = string.IsNullOrWhiteSpace(_locationName)
                    ? string.Empty
                    : $"📍 {_locationName}";

                if (result?.Category == null)
                {
                    ErrorMessage = "Could not identify the food. Please try a clearer photo.";
                    StatusText = "Take a photo of your food to identify it";
                    return;
                }

                FoodName = CapitaliseFirst(result.Category.Name);
                Calories = (int)(result.Nutrition?.Calories?.Value ?? 0);
                HasResult = true;
                StatusText = "Food identified! Log it or retake.";
            }
            catch (FeatureNotSupportedException)
            {
                ErrorMessage = "Camera is not supported on this device.";
            }
            catch (PermissionException)
            {
                ErrorMessage = "Camera permission was denied.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"[PictureViewModel] {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Geolocation ──────────────────────────────────────────────────

        private async Task<string> GetLocationNameAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) return string.Empty;

                var location = await Geolocation.Default.GetLastKnownLocationAsync()
                               ?? await Geolocation.Default.GetLocationAsync(
                                   new GeolocationRequest(GeolocationAccuracy.Low,
                                       TimeSpan.FromSeconds(5)));

                if (location == null) return string.Empty;

                var placemarks = await Geocoding.Default.GetPlacemarksAsync(
                    location.Latitude, location.Longitude);

                var place = placemarks?.FirstOrDefault();
                if (place == null) return string.Empty;

                return place.Locality
                    ?? place.SubAdminArea
                    ?? place.AdminArea
                    ?? place.CountryName
                    ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PictureViewModel] Location error: {ex.Message}");
                return string.Empty;
            }
        }

        // ── Log meal ─────────────────────────────────────────────────────

        private async Task LogMealAsync()
        {
            try
            {
                IsBusy = true;

                await DatabaseService.Instance.AddMealAsync(new MealLogEntry
                {
                    FoodName = FoodName,
                    Calories = Calories,
                    ImagePath = _capturedImagePath,
                    LoggedAt = DateTime.Now,
                    LocationName = _locationName
                });

                HapticFeedback.Default.Perform(HapticFeedbackType.Click);

                await Shell.Current.DisplayAlert(
                    "Meal Logged",
                    $"{FoodName} ({CaloriesLabel}) has been saved to your history.",
                    "OK");

                await Shell.Current.GoToAsync("//History");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not save meal: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string CapitaliseFirst(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

        // ── INotifyPropertyChanged ────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void RefreshCanExecute()
        {
            (TakePhotoCommand as Command)?.ChangeCanExecute();
            (LogMealCommand as Command)?.ChangeCanExecute();
            (RetakeCommand as Command)?.ChangeCanExecute();
            (ToggleFlashCommand as Command)?.ChangeCanExecute();
        }
    }
}