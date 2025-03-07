using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            if (value is double currentValue && 
                Application.Current.MainWindow.FindName("ProgressSlider") is Slider slider &&
                slider.ActualWidth > 0 &&
                slider.Maximum > 0)
            {
                return (currentValue / slider.Maximum) * slider.ActualWidth;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
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