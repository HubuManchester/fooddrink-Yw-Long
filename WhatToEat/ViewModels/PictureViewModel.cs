using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WhatToEat.Models;
using WhatToEat.Services;

namespace WhatToEat.ViewModels
{
    /// <summary>
    /// ViewModel for PicturePage.
    /// Hardware features used:
    ///   3. Camera  — CapturePhotoAsync (take food photo)
    ///   4. Flash   — Toggles device torch/flashlight via IFlashlight
    ///   Computer vision — Spoonacular image classification API
    /// </summary>
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
            }, () => !IsBusy);

            // ── Flash / Torch toggle command ──────────────────────────────
            ToggleFlashCommand = new Command(
                async () => await ToggleFlashAsync(),
                () => !IsBusy);
        }

        // ── Properties ───────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { Set(ref _isBusy, value); OnPropertyChanged(nameof(IsNotBusy)); RefreshCanExecute(); }
        }
        public bool IsNotBusy => !_isBusy;

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

        // ── Flash / Torch ─────────────────────────────────────────────────

        /// <summary>True while the device torch is on.</summary>
        private bool _isFlashOn;
        public bool IsFlashOn
        {
            get => _isFlashOn;
            set
            {
                Set(ref _isFlashOn, value);
                OnPropertyChanged(nameof(FlashIcon));
                OnPropertyChanged(nameof(FlashLabel));
            }
        }

        /// <summary>Icon text shown on the flash toggle button.</summary>
        public string FlashIcon => _isFlashOn ? "🔦" : "🔦";

        /// <summary>Label text shown on the flash toggle button.</summary>
        public string FlashLabel => _isFlashOn ? "Flash On" : "Flash Off";

        /// <summary>True when the device supports a torch/flashlight.</summary>
        private bool _isFlashSupported = true;
        public bool IsFlashSupported
        {
            get => _isFlashSupported;
            set => Set(ref _isFlashSupported, value);
        }

        // Keeps the captured file path for logging
        private string _capturedImagePath = string.Empty;

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand TakePhotoCommand { get; }
        public ICommand LogMealCommand { get; }
        public ICommand RetakeCommand { get; }

        /// <summary>Toggles the device torch on or off. Hardware feature: Flash/Flashlight.</summary>
        public ICommand ToggleFlashCommand { get; }

        // ── Flash toggle — Hardware feature 4 ────────────────────────────

        /// <summary>
        /// Turns the device torch on or off.
        /// Useful when photographing food in low-light environments.
        /// Turns the flash OFF automatically after a photo is taken.
        /// Falls back gracefully if the flashlight is not available on the device.
        /// Hardware feature: Flash / Flashlight.
        /// </summary>
        private async Task ToggleFlashAsync()
        {
            try
            {
                // Check whether the flashlight is available on this device
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

        // ── Camera — Hardware feature 3 ──────────────────────────────────
        private async Task TakePhotoAsync()
        {
            try
            {
                // Check camera permission
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    ErrorMessage = "Camera permission is required to take a photo.";
                    return;
                }

                // Open camera
                var photo = await MediaPicker.Default.CapturePhotoAsync(
                    new MediaPickerOptions { Title = "Take a photo of your food" });

                if (photo == null) return; // user cancelled

                // Auto-turn off flash once the photo is taken
                if (_isFlashOn)
                {
                    await Flashlight.Default.TurnOffAsync();
                    IsFlashOn = false;
                }

                IsBusy = true;
                ErrorMessage = string.Empty;
                StatusText = "Identifying your food...";

                // Show preview immediately
                _capturedImagePath = photo.FullPath;
                PhotoSource = ImageSource.FromFile(photo.FullPath);

                // Send to Spoonacular image classification (computer vision)
                await using var stream = await photo.OpenReadAsync();
                var result = await _mealService.ClassifyFoodImageAsync(stream, photo.FileName);

                if (result?.Category == null)
                {
                    ErrorMessage = "Could not identify the food. Please try a clearer photo.";
                    StatusText = "Take a photo of your food to identify it";
                    return;
                }

                // Populate results
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

        // ── Log meal to history ───────────────────────────────────────────
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
                    LoggedAt = DateTime.Now
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