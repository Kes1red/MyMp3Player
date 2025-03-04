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
            if (value == null || parameter == null) return false;
        
            if (!double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue))
                return false;

            string[] parameters = parameter.ToString().Split(' ');
            string operation = parameters[0].ToLower();

            try 
            {
                switch(operation)
                {
                    case "lessthan":
                        return doubleValue < double.Parse(parameters[1], CultureInfo.InvariantCulture);
                    case "greaterthan":
                        return doubleValue > double.Parse(parameters[1], CultureInfo.InvariantCulture);
                    case "between":
                        return doubleValue >= double.Parse(parameters[1], CultureInfo.InvariantCulture) && 
                               doubleValue <= double.Parse(parameters[2], CultureInfo.InvariantCulture);
                    case "equals":
                        return Math.Abs(doubleValue - double.Parse(parameters[1], CultureInfo.InvariantCulture)) < 0.001;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
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
}