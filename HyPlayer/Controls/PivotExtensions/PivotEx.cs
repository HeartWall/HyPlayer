﻿using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace HyPlayer.Controls
{
    public class PivotEx : Pivot
    {
        public PivotEx()
        {
            this.DefaultStyleKey = typeof(PivotEx);

            progressPropSet = ElementCompositionPreview.GetElementVisual(this).Compositor.CreatePropertySet();
            progressPropSet.InsertScalar("Progress", 0);
            progressPropSet.InsertScalar("OffsetY", 0);

            internalPropSet = ElementCompositionPreview.GetElementVisual(this).Compositor.CreatePropertySet();
            internalPropSet.InsertScalar("MaxHeaderScrollOffset", 0);

            UpdateInternalProgress();

            this.SelectionChanged += PivotEx_SelectionChanged;
            this.Unloaded += PivotEx_Unloaded;
            this.PivotItemUnloading += PivotEx_PivotItemUnloading;
            this.PivotItemLoaded += PivotEx_PivotItemLoaded;
        }

        private ScrollViewer currentScrollViewer;
        private CompositionPropertySet currentScrollPropSet;
        private ExpressionAnimation scrollProgressBind;
        private ExpressionAnimation offsetYBind;

        private double lastScrollOffsetY;
        private bool innerSet;
        private CompositionPropertySet internalPropSet;
        private CompositionPropertySet progressPropSet;

        private CancellationTokenSource cts;

        public double MaxHeaderScrollOffset
        {
            get { return (double)GetValue(MaxHeaderScrollOffsetProperty); }
            set { SetValue(MaxHeaderScrollOffsetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MaxHeaderScrollOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxHeaderScrollOffsetProperty =
            DependencyProperty.Register("MaxHeaderScrollOffset", typeof(double), typeof(PivotEx), new PropertyMetadata(0d, (s, a) =>
            {
                if (s is PivotEx sender)
                {
                    sender.internalPropSet.InsertScalar("MaxHeaderScrollOffset", Convert.ToSingle(a.NewValue));
                    sender.UpdateHeaderScrollOffset();
                    sender.UpdateInternalProgress();
                }
            }));



        public double HeaderScrollOffset
        {
            get { return (double)GetValue(HeaderScrollOffsetProperty); }
            private set { SetValue(HeaderScrollOffsetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TitleProgress.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeaderScrollOffsetProperty =
            DependencyProperty.Register("HeaderScrollOffset", typeof(double), typeof(PivotEx), new PropertyMetadata(0d, (s, a) =>
            {
                if (s is PivotEx sender)
                {
                    if (!sender.innerSet) throw new ArgumentException(nameof(HeaderScrollOffset));

                    sender.UpdateInternalProgress();
                }
            }));


        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            UpdateCurrentScrollViewer();
        }

        private async void UpdateCurrentScrollViewer()
        {
            var container = ContainerFromIndex(SelectedIndex) as PivotItem;

            var sv = container?.FindDescendant<ScrollViewer>();

            if (sv == currentScrollViewer) return;

            cts?.Cancel();
            cts = null;

            if (currentScrollViewer != null)
            {
                currentScrollViewer.ViewChanging -= CurrentScrollViewer_ViewChanging;
            }

            currentScrollViewer = sv;

            //progressPropSet.StopAnimation("Progress");
            //progressPropSet.StopAnimation("OffsetY");
            //progressPropSet.InsertScalar("Progress", (float)(MaxHeaderScrollOffset == 0 ? 0 : (Math.Clamp(lastScrollOffsetY, 0, MaxHeaderScrollOffset) / MaxHeaderScrollOffset)));
            //progressPropSet.InsertScalar("OffsetY", (float)(Math.Clamp(lastScrollOffsetY, 0, MaxHeaderScrollOffset)));

            scrollProgressBind = internalPropSet.Compositor.CreateExpressionAnimation("prop.Progress");
            scrollProgressBind.SetReferenceParameter("prop", internalPropSet);
            offsetYBind = internalPropSet.Compositor.CreateExpressionAnimation("prop.OffsetY");
            offsetYBind.SetReferenceParameter("prop", internalPropSet);

            progressPropSet.StartAnimation("OffsetY", offsetYBind);
            progressPropSet.StartAnimation("Progress", scrollProgressBind);

            if (currentScrollViewer != null)
            {
                var _cts = new CancellationTokenSource();
                cts = _cts;

                currentScrollViewer.ViewChanging += CurrentScrollViewer_ViewChanging;

                var offsetY = await TryScrollVerticalOffsetAsync(currentScrollViewer);

                if (cts.IsCancellationRequested) return;

                UpdateHeaderScrollOffset();

                currentScrollPropSet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(currentScrollViewer);

                await Task.Delay(200);

                if (cts.IsCancellationRequested) return;

                offsetYBind = currentScrollPropSet.Compositor.CreateExpressionAnimation("clamp(-scroll.Translation.Y, 0, prop.MaxHeaderScrollOffset)");
                offsetYBind.SetReferenceParameter("scroll", currentScrollPropSet);
                offsetYBind.SetReferenceParameter("prop", internalPropSet);

                progressPropSet.StartAnimation("OffsetY", offsetYBind);

                scrollProgressBind = currentScrollPropSet.Compositor.CreateExpressionAnimation("prop.MaxHeaderScrollOffset == 0 ? 0 : prop2.OffsetY / prop.MaxHeaderScrollOffset");
                scrollProgressBind.SetReferenceParameter("scroll", currentScrollPropSet);
                scrollProgressBind.SetReferenceParameter("prop", internalPropSet);
                scrollProgressBind.SetReferenceParameter("prop2", progressPropSet);

                progressPropSet.StartAnimation("Progress", scrollProgressBind);
            }
            else
            {
                currentScrollPropSet = null;
            }
        }

        private void CurrentScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            UpdateHeaderScrollOffset(e.NextView.VerticalOffset);
        }

        private void UpdateHeaderScrollOffset(double? verticalOffset = null)
        {
            innerSet = true;

            var oldValue = HeaderScrollOffset;
            try
            {
                var vt = verticalOffset ?? currentScrollViewer?.VerticalOffset ?? 0;
                lastScrollOffsetY = vt;
                HeaderScrollOffset = Math.Min(MaxHeaderScrollOffset, lastScrollOffsetY);
            }
            finally
            {
                innerSet = false;
            }

            if (oldValue != HeaderScrollOffset)
            {
                HeaderScrollOffsetChanged?.Invoke(this, EventArgs.Empty);
            }
        }


        private void PivotEx_PivotItemUnloading(Pivot sender, PivotItemEventArgs args)
        {
            Debug.WriteLine(SelectedIndex);
        }


        private void PivotEx_PivotItemLoaded(Pivot sender, PivotItemEventArgs args)
        {
            var sv = args.Item.FindDescendant<ScrollViewer>();
            if (sv != null)
            {
                TryScrollVerticalOffsetAsync(sv);
            }

            var container = this.ContainerFromIndex(SelectedIndex) as PivotItem;
            if (container == args.Item)
            {
                UpdateCurrentScrollViewer();
            }
        }

        private Task<double?> TryScrollVerticalOffsetAsync(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return null;

            double? offsetY = null;

            if (lastScrollOffsetY < MaxHeaderScrollOffset)
            {
                offsetY = Math.Min(MaxHeaderScrollOffset, lastScrollOffsetY);
            }
            else if (scrollViewer.VerticalOffset < MaxHeaderScrollOffset)
            {
                offsetY = MaxHeaderScrollOffset;
            }

            if (offsetY.HasValue)
            {
                if (scrollViewer.ChangeView(null, offsetY.Value, null, true))
                {
                    var tcs = new TaskCompletionSource<double?>();
                    scrollViewer.ViewChanged += ScrollViewer_ViewChanged;

                    return tcs.Task;

                    void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
                    {
                        scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                        tcs.SetResult(scrollViewer.VerticalOffset);
                    }
                }
                scrollViewer.UpdateLayout();
            }

            return Task.FromResult<double?>(null);
        }

        private void UpdateInternalProgress()
        {
            internalPropSet.InsertScalar("Progress", (float)(MaxHeaderScrollOffset == 0 ? 0 : (Math.Clamp(lastScrollOffsetY, 0, MaxHeaderScrollOffset) / MaxHeaderScrollOffset)));
            internalPropSet.InsertScalar("OffsetY", (float)(Math.Clamp(lastScrollOffsetY, 0, MaxHeaderScrollOffset)));
        }

        private void PivotEx_Unloaded(object sender, RoutedEventArgs e)
        {
            lastScrollOffsetY = 0;
        }

        private void PivotEx_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCurrentScrollViewer();
        }

        public CompositionPropertySet GetProgressPropertySet()
        {
            return progressPropSet;
        }

        public event EventHandler HeaderScrollOffsetChanged;

    }
}
