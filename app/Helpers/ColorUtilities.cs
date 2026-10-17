using System.Runtime.CompilerServices;

namespace GHelper.Helpers
{
    public class ColorUtils
    {
        // Method to get the weighted average between two colors
        public static Color GetWeightedAverage(Color color1, Color color2, float weight)
        {

            int red = (int)Math.Round(color1.R * (1 - weight) + color2.R * weight);
            int green = (int)Math.Round(color1.G * (1 - weight) + color2.G * weight);
            int blue = (int)Math.Round(color1.B * (1 - weight) + color2.B * weight);

            red = Math.Min(255, Math.Max(0, red));
            green = Math.Min(255, Math.Max(0, green));
            blue = Math.Min(255, Math.Max(0, blue));

            return Color.FromArgb(red, green, blue);
        }

        public static Color GetMidColor(Color color1, Color color2)
        {
            return Color.FromArgb((color1.R + color2.R) / 2,
                (color1.G + color2.G) / 2,
                (color1.B + color2.B) / 2);
        }

        public static Color GetDominantColor(Bitmap bmp)
        {

            //Used for tally
            int r = 0;
            int g = 0;
            int b = 0;

            int total = 0;

            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    Color clr = bmp.GetPixel(x, y);

                    r += clr.R;
                    g += clr.G;
                    b += clr.B;

                    total++;
                }
            }

            //Calculate average
            r /= total;
            g /= total;
            b /= total;

            return Color.FromArgb(r, g, b);
        }

        public class HSV
        {
            public double Hue { get; set; }
            public double Saturation { get; set; }
            public double Value { get; set; }

            public Color ToRGB()
            {
                var hue = Hue * 6;
                var saturation = Saturation;
                var value = Value;

                double red;
                double green;
                double blue;

                if (saturation == 0)
                {
                    red = green = blue = value;
                }
                else
                {
                    var i = Convert.ToInt32(Math.Floor(hue));
                    var f = hue - i;
                    var p = value * (1 - saturation);
                    var q = value * (1 - saturation * f);
                    var t = value * (1 - saturation * (1 - f));
                    int mod = i % 6;

                    red = new[] { value, q, p, p, t, value }[mod];
                    green = new[] { t, value, value, q, p, p }[mod];
                    blue = new[] { p, p, t, value, value, q }[mod];
                }

                return Color.FromArgb(Convert.ToInt32(red * 255), Convert.ToInt32(green * 255), Convert.ToInt32(blue * 255));
            }

            public static HSV ToHSV(Color rgb)
            {
                double red = rgb.R / 255.0;
                double green = rgb.G / 255.0;
                double blue = rgb.B / 255.0;
                var min = Math.Min(red, Math.Min(green, blue));
                var max = Math.Max(red, Math.Max(green, blue));
                var delta = max - min;
                double hue;
                double saturation = 0;
                var value = max;

                if (max != 0)
                    saturation = delta / max;

                if (delta == 0)
                    hue = 0;
                else
                {
                    if (red == max)
                        hue = (green - blue) / delta + (green < blue ? 6 : 0);
                    else if (green == max)
                        hue = 2 + (blue - red) / delta;
                    else
                        hue = 4 + (red - green) / delta;

                    hue /= 6;
                }

                return new HSV { Hue = hue, Saturation = saturation, Value = value };
            }

            public static Color UpSaturation(Color rgb, float increse = 0.2f) //make color more colored
            {
                if (rgb.R == rgb.G && rgb.G == rgb.B)
                    return rgb;
                var hsv_color = ToHSV(rgb);
                hsv_color.Saturation = Math.Min(hsv_color.Saturation + increse, 1.00f);
                return hsv_color.ToRGB();
            }

        }
    }

}