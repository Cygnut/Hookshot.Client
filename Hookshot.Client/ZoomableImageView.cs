using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace Hookshot.Client
{
    public class ZoomableImageView : View
    {
        public ZoomableImageView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            Initialize();
        }

        public ZoomableImageView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            Initialize();
        }

        private void Initialize()
        {
        }

        // Gesturing:
        // https://developer.xamarin.com/guides/cross-platform/application_fundamentals/touch/part_4_android_touch_walkthrough/
                
        static readonly int InvalidPointerId = -1;

        BitmapDrawable Image;
        Func<Bitmap> CreateDefaultImage;

        ScaleGestureDetector ScaleDetector;

        int ActivePointerId = InvalidPointerId;
        float LastTouchX;
        float LastTouchY;
        float PosX;
        float PosY;
        float ScaleFactor = 1.0f;
        
        class ScaleListener : ScaleGestureDetector.SimpleOnScaleGestureListener
        {
            readonly ZoomableImageView View;

            public ScaleListener(ZoomableImageView view)
            {
                View = view;
            }

            float ForceIntoRange(float value, float min, float max)
            {
                return Math.Min(Math.Max(value, min), max);
            }

            public override bool OnScale(ScaleGestureDetector detector)
            {
                View.ScaleFactor *= detector.ScaleFactor;
                // Put a limit on how small or big the image can get.
                View.ScaleFactor = ForceIntoRange(View.ScaleFactor, 0.1f, 5.0f);
                View.Invalidate();
                return true;
            }
        }

        BitmapDrawable FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null) return null;

            try
            {
                var draw = new BitmapDrawable(Resources, bitmap);
                draw.SetBounds(0, 0, draw.IntrinsicWidth, draw.IntrinsicHeight);
                return draw;
            }
            catch (Exception) { return null; }
        }

        public ZoomableImageView(Context context, Func<Bitmap> createDefaultImage) : 
            base(context, null, 0)
        {
            Initialize();

            CreateDefaultImage = createDefaultImage;
            ScaleDetector = new ScaleGestureDetector(context, new ScaleListener(this));
        }

        public void SetBitmap(Bitmap bitmap)
        {
            SetupBitmap(bitmap);
            Invalidate();
        }

        void SetupBitmap(Bitmap bitmap)
        {
            // Get rid of the old image.
            DestroyImage();

            // Set up the new image.
            Image = FromBitmap(bitmap);

            // Revert the zoom.
            PosX = PosY = 0;
            ScaleFactor = Image == null ? 1 : ComputeIdealScaleFactor(Image);
        }
        
        float ComputeIdealScaleFactor(BitmapDrawable drawable)
        {
            float xScale = Width / ((float)drawable.IntrinsicWidth);
            float yScale = Height / ((float)drawable.IntrinsicHeight);
            return Math.Min(xScale, yScale);
        }

        void DestroyImage()
        {
            try
            {
                Image?.Bitmap.Recycle();
                Image?.Dispose();
            }
            catch (Exception) { }
            finally
            {
                Image = null;
            }
        }

        public void OnDestroy()
        {
            SetupBitmap(null);
        }
        
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            if (Image == null)
                SetupBitmap(CreateDefaultImage());

            canvas.Save();
            canvas.Translate(PosX, PosY);
            canvas.Scale(ScaleFactor, ScaleFactor);
            Image?.Draw(canvas);
            canvas.Restore();
        }

        public override bool OnTouchEvent(MotionEvent ev)
        {
            ScaleDetector.OnTouchEvent(ev);

            MotionEventActions action = ev.Action & MotionEventActions.Mask;
            int pointerIndex;

            switch (action)
            {
                case MotionEventActions.Down:
                    LastTouchX = ev.GetX();
                    LastTouchY = ev.GetY();
                    ActivePointerId = ev.GetPointerId(0);
                    break;

                case MotionEventActions.Move:
                    pointerIndex = ev.FindPointerIndex(ActivePointerId);
                    float x = ev.GetX(pointerIndex);
                    float y = ev.GetY(pointerIndex);
                    if (!ScaleDetector.IsInProgress)
                    {
                        // Only move the ScaleGestureDetector isn't already processing a gesture.
                        float deltaX = x - LastTouchX;
                        float deltaY = y - LastTouchY;
                        PosX += deltaX;
                        PosY += deltaY;
                        Invalidate();
                    }

                    LastTouchX = x;
                    LastTouchY = y;
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    // We no longer need to keep track of the active pointer.
                    ActivePointerId = InvalidPointerId;
                    break;

                case MotionEventActions.PointerUp:
                    // check to make sure that the pointer that went up is for the gesture we're tracking.
                    pointerIndex = (int)(ev.Action & MotionEventActions.PointerIndexMask) >> (int)MotionEventActions.PointerIndexShift;
                    int pointerId = ev.GetPointerId(pointerIndex);
                    if (pointerId == ActivePointerId)
                    {
                        // This was our active pointer going up. Choose a new
                        // action pointer and adjust accordingly
                        int newPointerIndex = pointerIndex == 0 ? 1 : 0;
                        LastTouchX = ev.GetX(newPointerIndex);
                        LastTouchY = ev.GetY(newPointerIndex);
                        ActivePointerId = ev.GetPointerId(newPointerIndex);
                    }
                    break;

            }
            return true;
        }
    }
}