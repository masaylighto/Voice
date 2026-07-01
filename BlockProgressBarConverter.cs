using System;
using System.Globalization;
using System.Windows.Data;

namespace Voice
{
    public class BlockProgressBarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float score)
            {
                // Clip score between 0 and 100
                score = Math.Clamp(score, 0f, 100f);
                
                // Get number of filled blocks (0 to 10)
                int filledBlocks = (int)Math.Round(score / 10.0f);
                filledBlocks = Math.Clamp(filledBlocks, 0, 10);
                
                int emptyBlocks = 10 - filledBlocks;
                
                return new string('█', filledBlocks) + new string('░', emptyBlocks);
            }
            
            return "░░░░░░░░░░";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
