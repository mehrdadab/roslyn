﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.AddAccessibilityModifiers
{
    internal abstract class AbstractAddAccessibilityModifiersDiagnosticAnalyzer<TCompilationUnitSyntax>
        : AbstractBuiltInCodeStyleDiagnosticAnalyzer
        where TCompilationUnitSyntax : SyntaxNode
    {
        protected AbstractAddAccessibilityModifiersDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.AddAccessibilityModifiersDiagnosticId,
                   EnforceOnBuildValues.AddAccessibilityModifiers,
                   CodeStyleOptions2.AccessibilityModifiersRequired,
                   new LocalizableResourceString(nameof(AnalyzersResources.Add_accessibility_modifiers), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
                   new LocalizableResourceString(nameof(AnalyzersResources.Accessibility_modifiers_required), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)))
        {
        }

        public sealed override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SyntaxTreeWithoutSemanticsAnalysis;

        protected sealed override void InitializeWorker(AnalysisContext context)
            => context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);

        private void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
        {
            var option = context.GetAnalyzerOptions().RequireAccessibilityModifiers;
            if (option.Value == AccessibilityModifiersRequired.Never)
                return;

            ProcessCompilationUnit(context, option, (TCompilationUnitSyntax)context.Tree.GetRoot(context.CancellationToken));
        }

        protected abstract void ProcessCompilationUnit(SyntaxTreeAnalysisContext context, CodeStyleOption2<AccessibilityModifiersRequired> option, TCompilationUnitSyntax compilationUnitSyntax);
    }
}
