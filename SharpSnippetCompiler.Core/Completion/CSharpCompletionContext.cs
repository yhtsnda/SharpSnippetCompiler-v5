﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.IO;
using System.Text.RegularExpressions;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Completion;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.TypeSystem;

namespace ICSharpCode.SharpSnippetCompiler.Core.Completion
{
	public sealed class CSharpCompletionContext
	{
        public readonly IDocument OriginalDocument;
	    public readonly int OriginalOffset;

	    public readonly int Offset;
        public readonly IDocument Document;
        public readonly ICompilation Compilation;
		public readonly IProjectContent ProjectContent;
	    public readonly CSharpResolver Resolver;
		public readonly CSharpTypeResolveContext TypeResolveContextAtCaret;
		public readonly ICompletionContextProvider CompletionContextProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CSharpCompletionContext"/> class.
        /// </summary>
        /// <param name="document">The document, make sure the FileName property is set on the document.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="projectContent">Content of the project.</param>
        /// <param name="usings">The usings.</param>
        public CSharpCompletionContext(IDocument document, int offset, IProjectContent projectContent, string usings = null)
        {
            OriginalDocument = document;
            OriginalOffset = offset;

            //if the document is a c# script we have to soround the document with some code.
            Document = PrepareCompletionDocument(document, ref offset, usings);
            Offset = offset;

            var syntaxTree = new CSharpParser().Parse(Document, Document.FileName);
            syntaxTree.Freeze();
            var unresolvedFile = syntaxTree.ToTypeSystem();

            ProjectContent = projectContent.AddOrUpdateFiles(unresolvedFile);
            //note: it's important that the project content is used that is returned after adding the unresolved file
            Compilation = ProjectContent.CreateCompilation();

            var location = Document.GetLocation(Offset);
            Resolver = unresolvedFile.GetResolver(Compilation, location);
            TypeResolveContextAtCaret = unresolvedFile.GetTypeResolveContext(Compilation, location);
            CompletionContextProvider = new DefaultCompletionContextProvider(Document, unresolvedFile);
		}

        private static Regex replaceRegex = new Regex("[^a-zA-Z0-9_]");
        private static IDocument PrepareCompletionDocument(IDocument document, ref int offset, string usings = null)
        {
            if(String.IsNullOrEmpty(document.FileName))
                return document;

            //if the code is just a script it it will contain no namestpace, class and method structure and so the code completion will not work properly
            // for it to work we have to suround the code with the appropriate code structure
            //we only process the file if its a .csx file
            var fileExtension = Path.GetExtension(document.FileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(document.FileName);
            if(String.IsNullOrEmpty(fileExtension) || String.IsNullOrEmpty(fileNameWithoutExtension))
                return document;

            if (fileExtension.ToLower() == ".csx")
            {
                var className = replaceRegex.Replace(fileNameWithoutExtension, "");
                var header = "";
                header += (usings ?? "") + Environment.NewLine;
                header += "static class " + className + " {" + Environment.NewLine;
                header += "static void Script(){" + Environment.NewLine;
                var footer = "";
                footer += Environment.NewLine + "}}";

                var code = header + document.Text + footer;
                offset += header.Length;
                return new ReadOnlyDocument(new StringTextSource(code), document.FileName);
            }
            return document;
        }
	}
}
