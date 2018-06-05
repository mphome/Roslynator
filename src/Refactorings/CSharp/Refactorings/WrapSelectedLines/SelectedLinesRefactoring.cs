﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Refactorings.WrapSelectedLines;
using Roslynator.Text;

namespace Roslynator.CSharp.Refactorings
{
    internal abstract class SelectedLinesRefactoring
    {
        public abstract ImmutableArray<TextChange> GetTextChanges(IEnumerable<TextLine> selectedLines);

        public Task<Document> RefactorAsync(
            Document document,
            TextLineCollectionSelection selectedLines,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ImmutableArray<TextChange> textChanges = GetTextChanges(selectedLines);

            return document.WithTextChangesAsync(textChanges, cancellationToken);
        }

        public static async Task ComputeRefactoringsAsync(RefactoringContext context, SyntaxNode node)
        {
            if (context.IsAnyRefactoringEnabled(
                RefactoringIdentifiers.WrapInRegion,
                RefactoringIdentifiers.WrapInIfDirective,
                RefactoringIdentifiers.RemoveEmptyLines))
            {
                SyntaxNode root = context.Root;
                TextSpan span = context.Span;

                if (!IsFullLineSpan(node, span))
                    return;

                Document document = context.Document;
                SourceText sourceText = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);

                if (!TextLineCollectionSelection.TryCreate(sourceText.Lines, span, out TextLineCollectionSelection selectedLines))
                    return;

                if (!IsInMultiLineDocumentationComment(root, span.Start)
                    && !IsInMultiLineDocumentationComment(root, span.End))
                {
                    if (context.IsRefactoringEnabled(RefactoringIdentifiers.WrapInRegion))
                    {
                        context.RegisterRefactoring(
                            "Wrap in region",
                            ct => WrapInRegionRefactoring.Instance.RefactorAsync(document, selectedLines, ct),
                            RefactoringIdentifiers.WrapInRegion);
                    }

                    if (context.IsRefactoringEnabled(RefactoringIdentifiers.WrapInIfDirective))
                    {
                        context.RegisterRefactoring(
                            "Wrap in #if",
                            ct => WrapInIfDirectiveRefactoring.Instance.RefactorAsync(document, selectedLines, ct),
                            RefactoringIdentifiers.WrapInIfDirective);
                    }
                }

                if (context.IsRefactoringEnabled(RefactoringIdentifiers.RemoveEmptyLines))
                {
                    bool containsEmptyLine = false;

                    foreach (TextLine line in selectedLines)
                    {
                        if (line.IsEmptyOrWhiteSpace()
                            && root.FindTrivia(line.End, findInsideTrivia: true).IsEndOfLineTrivia())
                        {
                            containsEmptyLine = true;
                            break;
                        }
                    }

                    if (containsEmptyLine)
                    {
                        context.RegisterRefactoring(
                            "Remove empty lines",
                            ct =>
                            {
                                IEnumerable<TextChange> textChanges = selectedLines
                                    .Where(line => line.IsEmptyOrWhiteSpace() && root.FindTrivia(line.End, findInsideTrivia: true).IsEndOfLineTrivia())
                                    .Select(line => new TextChange(line.SpanIncludingLineBreak, ""));

                                return document.WithTextChangesAsync(textChanges, ct);
                            },
                            RefactoringIdentifiers.RemoveEmptyLines);
                    }
                }
            }
        }

        private static bool IsFullLineSpan(SyntaxNode node, TextSpan span)
        {
            if (!node.FullSpan.Contains(span))
                throw new ArgumentOutOfRangeException(nameof(span), span, "");

            if (!span.IsEmpty)
            {
                if (span.Start == 0
                    || IsStartOrEndOfLine(span.Start, -1))
                {
                    return IsStartOrEndOfLine(span.End, -1)
                        || IsStartOrEndOfLine(span.End);
                }
            }

            return false;

            bool IsStartOrEndOfLine(int position, int offset = 0)
            {
                SyntaxTrivia trivia = node.FindTrivia(position + offset);

                if (trivia.IsEndOfLineTrivia()
                    && trivia.Span.End == position)
                {
                    return true;
                }

                SyntaxToken token = node.FindToken(position + offset, findInsideTrivia: true);

                return token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken)
                    && token.Span.End == position;
            }
        }

        private static bool IsInMultiLineDocumentationComment(SyntaxNode root, int position)
        {
            SyntaxToken token = root.FindToken(position, findInsideTrivia: true);

            for (SyntaxNode node = token.Parent; node != null; node = node.Parent)
            {
                if (node.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                    return true;
            }

            return false;
        }
    }
}
