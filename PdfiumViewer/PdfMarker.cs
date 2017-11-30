using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

#pragma warning disable 1591

namespace PdfiumViewer
{
    public class PdfMarker : IPdfMarker
    {
        public int Page { get; }
        public RectangleF Bounds { get; }
        public Color Color { get; }
        public Color BorderColor { get; }
        public float BorderWidth { get; }
        public PdfRotation RotationAtCreation { get; }

        public PdfMarker(int page, RectangleF bounds, Color color, PdfRotation boundsRelativeTo = PdfRotation.Rotate0)
            : this(page, bounds, color, Color.Transparent, 0, boundsRelativeTo)
        {
        }

        public PdfMarker(int page, RectangleF bounds, Color color, Color borderColor, float borderWidth,
            PdfRotation boundsRelativeTo = PdfRotation.Rotate0)
        {
            Page = page;
            Bounds = bounds;
            Color = color;
            BorderColor = borderColor;
            BorderWidth = borderWidth;
            RotationAtCreation = boundsRelativeTo;
        }

        public void Draw(PdfRenderer renderer, Graphics graphics)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));

            var rotatedBounds = GetBoundsForCurrentRotation(renderer);
            var bounds = renderer.BoundsFromPdf(new PdfRectangle(Page, rotatedBounds));

            using (var brush = new SolidBrush(Color))
            {
                graphics.FillRectangle(brush, bounds);
            }

            if (BorderWidth > 0)
            {
                using (var pen = new Pen(BorderColor, BorderWidth))
                {
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
            }
        }

        private RectangleF GetBoundsForCurrentRotation(PdfRenderer renderer)
        {
            var unrotatedPageSize = renderer.Document.PageSizes[Page];
            var pageSizeAtCreation = Utilities.TranslateSize(unrotatedPageSize, RotationAtCreation);
            PdfRotation diffRotation = Utilities.GetDiffRotation(RotationAtCreation, renderer.Rotation);
            return Utilities.GetRotatedRectangle(Bounds, pageSizeAtCreation, diffRotation);
        }

        private static class Utilities
        {
            /// <summary>
            /// Return Size (height/width) adjusted for a rotation
            /// </summary>
            public static SizeF TranslateSize(SizeF size, PdfRotation rotation)
            {
                switch (rotation)
                {
                    case PdfRotation.Rotate90:
                    case PdfRotation.Rotate270:
                        return new SizeF(size.Height, size.Width);

                    default:
                        return size;
                }
            }

            /// <summary>
            /// Returns the amount of rotation needed to go from <paramref name="oldRotation"/>
            /// to <paramref name="newRotation"/>
            /// </summary>
            public static PdfRotation GetDiffRotation(PdfRotation oldRotation, PdfRotation newRotation)
            {
                const int numberOfRotationSteps = 4;
                int result = Modulo(newRotation - oldRotation, numberOfRotationSteps);
                return (PdfRotation)result;
            }

            /// <summary>
            /// Returns the positive remainder after dividing <paramref name="amount"/> 
            /// by <paramref name="modulus"/>
            /// </summary>
            /// <remarks>
            /// Needed because '%' by itself can return a negative result, e.g. -5 % 4 = -1
            /// </remarks>
            private static int Modulo(int amount, int modulus)
            {
                return ((amount % modulus) + modulus) % modulus;
            }

            /// <summary>
            /// Applies rotation to a PDF rectangle
            /// </summary>
            /// <param name="rect">Target rectangle, for current orientation</param>
            /// <param name="pageSize">Page size, for current orientation</param>
            /// <param name="rotation">
            /// Degrees of rotation to apply, expressed as angle relative to 0
            /// (Rotate0 is a no-op)
            /// </param>
            /// <returns></returns>
            public static RectangleF GetRotatedRectangle(RectangleF rect, SizeF pageSize, PdfRotation rotation)
            {
                if (rotation == PdfRotation.Rotate0) return rect;

                // Any two opposite corners define a rectangle.
                // We pick two, rotate them, and those define new rect.
                var pointA = new PointF(rect.Left, rect.Bottom);
                var pointB = new PointF(rect.Right, rect.Top);

                var rotA = GetRotatedPoint(pointA, pageSize, rotation);
                var rotB = GetRotatedPoint(pointB, pageSize, rotation);

                return RectFromPoints(rotA, rotB);
            }

            /// <summary>
            /// Applies rotation to a PDF point
            /// </summary>
            /// <param name="orig">Unrotated point, in PDF coordinates</param>
            /// <param name="pageSize">Unrotated page size</param>
            /// <param name="rotationToApply"></param>
            /// <returns></returns>
            public static PointF GetRotatedPoint(PointF orig, SizeF pageSize, PdfRotation rotationToApply)
            {
                if (rotationToApply == PdfRotation.Rotate0) return orig;

                float origX = orig.X;
                float origY = orig.Y;
                float minusX = pageSize.Width - orig.X;
                float minusY = pageSize.Height - orig.Y;
                PointF newPoint;

                switch (rotationToApply)
                {
                    case PdfRotation.Rotate90:
                        newPoint = new PointF(x: origY, y: minusX);
                        break;
                    case PdfRotation.Rotate180:
                        newPoint = new PointF(x: minusX, y: minusY);
                        break;
                    case PdfRotation.Rotate270:
                        newPoint = new PointF(x: minusY, y: origX);
                        break;
                    default:
                        return orig;
                }

                return newPoint;
            }

            // Origin is bottom-left (PDF coords)
            private static RectangleF RectFromPoints(PointF a, PointF b)
            {
                PointF bottomLeft = new PointF(x: Math.Min(a.X, b.X), y: Math.Min(a.Y, b.Y));
                SizeF size = new SizeF(width: Math.Abs(a.X - b.X), height: (Math.Abs(a.Y - b.Y)));
                return new RectangleF(bottomLeft, size);
            }
        }
    }
}
