using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace MyMp3Player.Converters
{
    /// <summary>
    /// Конвертер для преобразования значения слайдера в ширину индикатора прогресса
    /// </summary>
    public class SliderValueToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderValue)
            {
                var slider = parameter as Slider;
                
                if (slider == null)
                {
                    // Получаем родительский элемент (слайдер) через RelativeSource
                    var element = value as DependencyObject;
                    while (element != null && !(element is Slider))
                    {
                        element = VisualTreeHelper.GetParent(element);
                    }
                    slider = element as Slider;
                }
                
                if (slider != null && slider.ActualWidth > 0)
                {
                    // Вычисляем ширину индикатора прогресса на основе значения слайдера
                    double percent = (sliderValue - slider.Minimum) / (slider.Maximum - slider.Minimum);
                    return slider.ActualWidth * percent;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для преобразования значения слайдера в позицию ползунка
    /// </summary>
    public class SliderValueToPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderValue)
            {
                var slider = parameter as Slider;
                
                if (slider == null)
                {
                    // Получаем родительский элемент (слайдер) через RelativeSource
                    DependencyObject element = value as DependencyObject;
                    while (element != null && !(element is Slider))
                    {
                        element = VisualTreeHelper.GetParent(element);
                    }
                    slider = element as Slider;
                }
                
                if (slider != null && slider.ActualWidth > 0)
                {
                    // Вычисляем позицию ползунка на основе значения слайдера
                    double percent = (sliderValue - slider.Minimum) / (slider.Maximum - slider.Minimum);
                    return slider.ActualWidth * percent;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}