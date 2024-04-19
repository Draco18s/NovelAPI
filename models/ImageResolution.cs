using System;

namespace net.novelai.api
{
    public class ImageResolution
    {
        public static ImageSize WallpaperPortrait = new ImageSize(1088, 1920);
        public static ImageSize WallpaperLandscape = new ImageSize(1920, 1088);

        // v1
        public static ImageSize SmallPortrait = new ImageSize(384, 640);
        public static ImageSize SmallLandscape = new ImageSize(640, 384);
        public static ImageSize SmallSquare = new ImageSize(512, 512);

        public static ImageSize NormalPortrait = new ImageSize(512, 768);
        public static ImageSize NormalLandscape = new ImageSize(768, 512);
        public static ImageSize NormalSquare = new ImageSize(640, 640);

        public static ImageSize LargePortrait = new ImageSize(512, 1024);
        public static ImageSize LargeLandscape = new ImageSize(1024, 512);
        public static ImageSize LargeSquare = new ImageSize(1024, 1024);

        // v2
        public static ImageSize SmallPortraitV2 = new ImageSize(512, 768);
        public static ImageSize SmallLandscapeV2 = new ImageSize(768, 512);
        public static ImageSize SmallSquareV2 = new ImageSize(640, 640);

        public static ImageSize NormalPortraitV2 = new ImageSize(832, 1216);
        public static ImageSize NormalLandscapeV2 = new ImageSize(1216, 832);
        public static ImageSize NormalSquareV2 = new ImageSize(1024, 1024);

        public static ImageSize LargePortraitV2 = new ImageSize(1024, 1536);
        public static ImageSize LargeLandscapeV2 = new ImageSize(1536, 1024);
        public static ImageSize LargeSquareV2 = new ImageSize(1472, 1472);

        // v3
        public static ImageSize SmallPortraitV3 = new ImageSize(512, 768);
        public static ImageSize SmallLandscapeV3 = new ImageSize(768, 512);
        public static ImageSize SmallSquareV3 = new ImageSize(640, 640);

        public static ImageSize NormalPortraitV3 = new ImageSize(832, 1216);
        public static ImageSize NormalLandscapeV3 = new ImageSize(1216, 832);
        public static ImageSize NormalSquareV3 = new ImageSize(1024, 1024);

        public static ImageSize LargePortraitV3 = new ImageSize(1024, 1536);
        public static ImageSize LargeLandscapeV3 = new ImageSize(1536, 1024);
        public static ImageSize LargeSquareV3 = new ImageSize(1472, 1472);

        public class ImageSize
        {
            public int Width { get; set; }
            public int Height { get; set; }

            public ImageSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }
    }
}