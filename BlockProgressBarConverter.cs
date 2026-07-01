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
                
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 10; i++)
                {
                    if (i < filledBlocks)
                        sb.Append("█ ");
                    else
                        sb.Append("░ ");
                }
                return sb.ToString().TrimEnd();
            }
            
            return "░ ░ ░ ░ ░ ░ ░ ░ ░ ░";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
