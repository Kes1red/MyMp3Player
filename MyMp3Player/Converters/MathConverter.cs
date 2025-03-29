using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MyMp3Player.Converters
{
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // Обработка числовых операций
            if (value is double doubleValue && parameter is string paramString)
            {
                // Проверка на операции сравнения
                if (paramString.StartsWith("LessThan:"))
                {
                    double compareValue = double.Parse(paramString.Substring(9), CultureInfo.InvariantCulture);
                    return doubleValue < compareValue;
                }
                else if (paramString.StartsWith("LessThanOrEqual:"))
                {
                    double compareValue = double.Parse(paramString.Substring(16), CultureInfo.InvariantCulture);
                    return doubleValue <= compareValue;
                }
                else if (paramString.StartsWith("GreaterThan:"))
                {
                    double compareValue = double.Parse(paramString.Substring(12), CultureInfo.InvariantCulture);
                    return doubleValue > compareValue;
                }
                else if (paramString.StartsWith("GreaterThanOrEqual:"))
                {
                    double compareValue = double.Parse(paramString.Substring(19), CultureInfo.InvariantCulture);
                    return doubleValue >= compareValue;
                }
                else if (paramString.StartsWith("Between:"))
                {
                    string[] values = paramString.Substring(8).Split(':');
                    if (values.Length == 2)
                    {
                        double min = double.Parse(values[0], CultureInfo.InvariantCulture);
                        double max = double.Parse(values[1], CultureInfo.InvariantCulture);
                        return doubleValue > min && doubleValue <= max;
                    }
                }
                else if (double.TryParse(paramString, out double multiplier))
                {
                    // Умножение для преобразования значения слайдера в ширину
                    return doubleValue * multiplier;
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }
    
    public class DurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string duration && TimeSpan.TryParse(duration, out var time))
            {
                return time.ToString(@"mm\:ss");
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double currentValue && currentValue > 0)
            {
                var slider = Application.Current.MainWindow.FindName("ProgressSlider") as Slider;
                return (currentValue / slider.Maximum) * slider.ActualWidth;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToShuffleIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                var resourceKey = isActive ? "ShuffleActiveIcon" : "ShuffleInactiveIcon";
                return Application.Current.Resources[resourceKey];
            }
            return Application.Current.Resources["ShuffleInactiveIcon"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? 1.0 : 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RepeatIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RepeatMode mode)
            {
                return mode switch
                {
                    RepeatMode.RepeatAll => Application.Current.FindResource("RepeatAllIcon"),
                    RepeatMode.RepeatOne => Application.Current.FindResource("RepeatOneIcon"),
                    _ => Application.Current.FindResource("RepeatIcon")
                };
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class RepeatModeToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (RepeatMode)value != RepeatMode.NoRepeat ? 1.0 : 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Enum.Parse(targetType, parameter.ToString()) : null;
        }
    }
    

        public class BoolToBackgroundConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is bool isEnabled && isEnabled)
                {
                    // Возвращаем зеленый цвет Spotify при активном состоянии
                    return new SolidColorBrush(Color.FromRgb(29, 185, 84)); // Зеленый цвет Spotify
                }
            
                // Возвращаем прозрачный фон при неактивном состоянии
                return new SolidColorBrush(Colors.Transparent);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    
    public class VolumeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double volume)
            {
                // Получаем экземпляр MainWindow для доступа к IsMuted
                var mainWindow = Application.Current.MainWindow as MainWindow;
                bool isMuted = mainWindow?.IsMuted ?? false;
                
                // Определяем ключ ресурса в зависимости от состояния
                string resourceKey;
                
                if (isMuted || volume <= 0)
                {
                    resourceKey = "VolumeMuteIcon";
                }
                else if (volume < 0.3)
                {
                    resourceKey = "VolumeLowIcon";
                }
                else if (volume < 0.7)
                {
                    resourceKey = "VolumeMediumIcon";
                }
                else
                {
                    resourceKey = "VolumeHighIcon";
                }
                
                // Получаем ресурс по ключу
                if (Application.Current.Resources.Contains(resourceKey))
                {
                    return Application.Current.Resources[resourceKey];
                }
            }
            
            // Возвращаем запасной вариант, если ресурс не найден
            return Application.Current.Resources["VolumeMuteIcon"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}