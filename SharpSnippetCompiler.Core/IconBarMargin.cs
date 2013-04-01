﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Debugging;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.Bookmarks;
using ICSharpCode.SharpDevelop.Workbench;

namespace ICSharpCode.SharpSnippetCompiler.Core
{
    /// <summary>
    ///     Icon bar: contains breakpoints and other icons.
    /// </summary>
    public class IconBarMargin : AbstractMargin, IDisposable
    {
        private readonly IBookmarkMargin manager;
        private IBookmark dragDropBookmark; // bookmark being dragged (!=null if drag'n'drop is active)
        private double dragDropCurrentPoint;
        private double dragDropStartPoint;
        private bool dragStarted; // whether drag'n'drop operation has started (mouse was moved minimum distance)

        public IconBarMargin(IBookmarkMargin manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");
            this.manager = manager;
        }

        #region OnTextViewChanged

        public virtual void Dispose()
        {
            TextView = null; // detach from TextView (will also detach from manager)
        }

        /// <inheritdoc />
        protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
        {
            if (oldTextView != null)
            {
                oldTextView.VisualLinesChanged -= OnRedrawRequested;
                manager.RedrawRequested -= OnRedrawRequested;
            }
            base.OnTextViewChanged(oldTextView, newTextView);
            if (newTextView != null)
            {
                newTextView.VisualLinesChanged += OnRedrawRequested;
                manager.RedrawRequested += OnRedrawRequested;
            }
            InvalidateVisual();
        }

        private void OnRedrawRequested(object sender, EventArgs e)
        {
            // Don't invalidate the IconBarMargin if it'll be invalidated again once the
            // visual lines become valid.
            if (TextView != null && TextView.VisualLinesValid)
            {
                InvalidateVisual();
            }
        }

        #endregion

        /// <inheritdoc />
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            // accept clicks even when clicking on the background
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(18, 0);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Size renderSize = RenderSize;
            drawingContext.DrawRectangle(SystemColors.ControlBrush, null,
                                         new Rect(0, 0, renderSize.Width, renderSize.Height));
            drawingContext.DrawLine(new Pen(SystemColors.ControlDarkBrush, 1),
                                    new Point(renderSize.Width - 0.5, 0),
                                    new Point(renderSize.Width - 0.5, renderSize.Height));

            TextView textView = TextView;
            if (textView != null && textView.VisualLinesValid)
            {
                // create a dictionary line number => first bookmark
                var bookmarkDict = new Dictionary<int, IBookmark>();
                foreach (IBookmark bm in manager.Bookmarks)
                {
                    int line = bm.LineNumber;
                    IBookmark existingBookmark;
                    if (!bookmarkDict.TryGetValue(line, out existingBookmark) || bm.ZOrder > existingBookmark.ZOrder)
                        bookmarkDict[line] = bm;
                }
                Size pixelSize = PixelSnapHelpers.GetPixelSize(this);
                foreach (VisualLine line in textView.VisualLines)
                {
                    int lineNumber = line.FirstDocumentLine.LineNumber;
                    IBookmark bm;
                    if (bookmarkDict.TryGetValue(lineNumber, out bm))
                    {
                        double lineMiddle =
                            line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextMiddle) -
                            textView.VerticalOffset;
                        var rect = new Rect(0, PixelSnapHelpers.Round(lineMiddle - 8, pixelSize.Height), 16, 16);
                        if (dragDropBookmark == bm && dragStarted)
                            drawingContext.PushOpacity(0.5);
                        drawingContext.DrawImage((bm.Image ?? BookmarkBase.DefaultBookmarkImage).ImageSource, rect);
                        if (dragDropBookmark == bm && dragStarted)
                            drawingContext.Pop();
                    }
                }
                if (dragDropBookmark != null && dragStarted)
                {
                    var rect = new Rect(0, PixelSnapHelpers.Round(dragDropCurrentPoint - 8, pixelSize.Height), 16, 16);
                    drawingContext.DrawImage((dragDropBookmark.Image ?? BookmarkBase.DefaultBookmarkImage).ImageSource,
                                             rect);
                }
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            CancelDragDrop();
            base.OnMouseDown(e);
            int line = GetLineFromMousePosition(e);
            if (!e.Handled && line > 0)
            {
                IBookmark bm = GetBookmarkFromLine(line);
                if (bm != null)
                {
                    bm.MouseDown(e);
                    if (!e.Handled)
                    {
                        if (e.ChangedButton == MouseButton.Left && bm.CanDragDrop && CaptureMouse())
                        {
                            StartDragDrop(bm, e);
                            e.Handled = true;
                        }
                    }
                }
            }
            // don't allow selecting text through the IconBarMargin
            if (e.ChangedButton == MouseButton.Left)
                e.Handled = true;
        }

        private IBookmark GetBookmarkFromLine(int line)
        {
            IBookmark result = null;
            foreach (IBookmark bm in manager.Bookmarks)
            {
                if (bm.LineNumber == line)
                {
                    if (result == null || bm.ZOrder > result.ZOrder)
                        result = bm;
                }
            }
            return result;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            CancelDragDrop();
            base.OnLostMouseCapture(e);
        }

        private void StartDragDrop(IBookmark bm, MouseEventArgs e)
        {
            dragDropBookmark = bm;
            dragDropStartPoint = dragDropCurrentPoint = e.GetPosition(this).Y;
            if (TextView != null)
            {
                var area = TextView.Services.GetService(typeof (TextArea)) as TextArea;
                if (area != null)
                    area.PreviewKeyDown += TextArea_PreviewKeyDown;
            }
        }

        private void CancelDragDrop()
        {
            if (dragDropBookmark != null)
            {
                dragDropBookmark = null;
                dragStarted = false;
                if (TextView != null)
                {
                    var area = TextView.Services.GetService(typeof (TextArea)) as TextArea;
                    if (area != null)
                        area.PreviewKeyDown -= TextArea_PreviewKeyDown;
                }
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        private void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // any key press cancels drag'n'drop
            CancelDragDrop();
            if (e.Key == Key.Escape)
                e.Handled = true;
        }

        private int GetLineFromMousePosition(MouseEventArgs e)
        {
            TextView textView = TextView;
            if (textView == null)
                return 0;
            VisualLine vl = textView.GetVisualLineFromVisualTop(e.GetPosition(textView).Y + textView.ScrollOffset.Y);
            if (vl == null)
                return 0;
            return vl.FirstDocumentLine.LineNumber;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragDropBookmark != null)
            {
                dragDropCurrentPoint = e.GetPosition(this).Y;
                if (Math.Abs(dragDropCurrentPoint - dragDropStartPoint) > SystemParameters.MinimumVerticalDragDistance)
                    dragStarted = true;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            int line = GetLineFromMousePosition(e);
            if (!e.Handled && dragDropBookmark != null)
            {
                if (dragStarted)
                {
                    if (line != 0)
                        dragDropBookmark.Drop(line);
                    e.Handled = true;
                }
                CancelDragDrop();
            }
            if (!e.Handled && line != 0)
            {
                IBookmark bm = GetBookmarkFromLine(line);
                if (bm != null)
                {
                    bm.MouseUp(e);
                    if (e.Handled)
                        return;
                }
                if (e.ChangedButton == MouseButton.Left && TextView != null)
                {
                    // no bookmark on the line: create a new breakpoint
                    var textEditor = TextView.Services.GetService(typeof (ITextEditor)) as ITextEditor;
                    if (textEditor != null)
                    {
                        DebuggerService.ToggleBreakpointAt(textEditor, line);
                        return;
                    }

                    // create breakpoint for the other posible active contents
                    var viewContent = SD.Workbench.ActiveContent as AbstractViewContentWithoutFile;
                    if (viewContent != null)
                    {
                        textEditor = viewContent.Services.GetService(typeof (ITextEditor)) as ITextEditor;
                        if (textEditor != null)
                        {
                            DebuggerService.ToggleBreakpointAt(textEditor, line);
                            return;
                        }
                    }
                }
            }
        }
    }
}