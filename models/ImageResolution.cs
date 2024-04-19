using System.Drawing;

namespace net.novelai.api
{
    public class ImageResolution
    {
        public static Size WallpaperPortrait = new Size(1088, 1920);
        public static Size WallpaperLandscape = new Size(1920, 1088);

        // v1
        public static Size SmallPortrait = new Size(384, 640);
        public static Size SmallLandscape = new Size(640, 384);
        public static Size SmallSquare = new Size(512, 512);

        public static Size NormalPortrait = new Size(512, 768);
        public static Size NormalLandscape = new Size(768, 512);
        public static Size NormalSquare = new Size(640, 640);

        public static Size LargePortrait = new Size(512, 1024);
        public static Size LargeLandscape = new Size(1024, 512);
        public static Size LargeSquare = new Size(1024, 1024);

        // v2
        public static Size SmallPortraitV2 = new Size(512, 768);
        public static Size SmallLandscapeV2 = new Size(768, 512);
        public static Size SmallSquareV2 = new Size(640, 640);

        public static Size NormalPortraitV2 = new Size(832, 1216);
        public static Size NormalLandscapeV2 = new Size(1216, 832);
        public static Size NormalSquareV2 = new Size(1024, 1024);

        public static Size LargePortraitV2 = new Size(1024, 1536);
        public static Size LargeLandscapeV2 = new Size(1536, 1024);
        public static Size LargeSquareV2 = new Size(1472, 1472);

        // v3
        public static Size SmallPortraitV3 = new Size(512, 768);
        public static Size SmallLandscapeV3 = new Size(768, 512);
        public static Size SmallSquareV3 = new Size(640, 640);

        public static Size NormalPortraitV3 = new Size(832, 1216);
        public static Size NormalLandscapeV3 = new Size(1216, 832);
        public static Size NormalSquareV3 = new Size(1024, 1024);

        public static Size LargePortraitV3 = new Size(1024, 1536);
        public static Size LargeLandscapeV3 = new Size(1536, 1024);
        public static Size LargeSquareV3 = new Size(1472, 1472);
    }
}