﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Editor;

namespace ICSharpCode.SharpSnippetCompiler.Core.Completion
{
    /// <summary>
    /// Output formatter that creates a dictionary from AST nodes to segments in the output text.
    /// </summary>
    public class SegmentTrackingOutputFormatter : TextWriterOutputFormatter
    {
        Dictionary<AstNode, ISegment> segments = new Dictionary<AstNode, ISegment>();
        Stack<int> startOffsets = new Stack<int>();
        readonly StringWriter stringWriter;

        public IDictionary<AstNode, ISegment> Segments
        {
            get { return segments; }
        }

        public SegmentTrackingOutputFormatter(StringWriter stringWriter)
            : base(stringWriter)
        {
            this.stringWriter = stringWriter;
        }

        public static IDictionary<AstNode, ISegment> WriteNode(StringWriter writer, AstNode node, CSharpFormattingOptions policy, ICSharpCode.AvalonEdit.TextEditorOptions options)
        {
            var formatter = new SegmentTrackingOutputFormatter(writer);
            formatter.IndentationString = options.IndentationString;
            var visitor = new CSharpOutputVisitor(formatter, policy);
            node.AcceptVisitor(visitor);
            return formatter.Segments;
        }

        public override void StartNode(AstNode node)
        {
            base.StartNode(node);
            startOffsets.Push(stringWriter.GetStringBuilder().Length);
        }

        public override void EndNode(AstNode node)
        {
            int startOffset = startOffsets.Pop();
            StringBuilder b = stringWriter.GetStringBuilder();
            int endOffset = b.Length;
            while (endOffset > 0 && b[endOffset - 1] == '\r' || b[endOffset - 1] == '\n')
                endOffset--;
            segments.Add(node, new TextSegment { StartOffset = startOffset, EndOffset = endOffset });
            base.EndNode(node);
        }
    }
}
