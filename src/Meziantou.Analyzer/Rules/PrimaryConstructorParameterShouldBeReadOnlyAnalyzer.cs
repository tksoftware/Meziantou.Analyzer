﻿#if CSHARP12_OR_GREATER
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Meziantou.Analyzer.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimaryConstructorParameterShouldBeReadOnlyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor s_rule = new(
        RuleIdentifiers.PrimaryConstructorParameterShouldBeReadOnly,
        title: "Primary constructor parameters should be readonly",
        messageFormat: "Primary constructor parameters should be readonly",
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "",
        helpLinkUri: RuleIdentifiers.GetHelpUri(RuleIdentifiers.PrimaryConstructorParameterShouldBeReadOnly));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.GetCSharpLanguageVersion() < Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12)
                return;

            context.RegisterOperationAction(AnalyzerAssignment, OperationKind.SimpleAssignment);
            context.RegisterOperationAction(AnalyzerAssignment, OperationKind.CompoundAssignment);
            context.RegisterOperationAction(AnalyzerAssignment, OperationKind.CoalesceAssignment);
            context.RegisterOperationAction(AnalyzerAssignment, OperationKind.DeconstructionAssignment);
            context.RegisterOperationAction(AnalyzerIncrementOrDecrement, OperationKind.Increment);
            context.RegisterOperationAction(AnalyzerIncrementOrDecrement, OperationKind.Decrement);
        });
    }

    private void AnalyzerIncrementOrDecrement(OperationAnalysisContext context)
    {
        var operation = (IIncrementOrDecrementOperation)context.Operation;
        var target = operation.Target;

        if (IsPrimaryConstructorParameter(target, context.CancellationToken))
        {
            context.ReportDiagnostic(s_rule, target);
        }
    }

    private void AnalyzerAssignment(OperationAnalysisContext context)
    {
        var operation = (IAssignmentOperation)context.Operation;
        var target = operation.Target;
        if (target is ITupleOperation)
        {
            foreach (var innerTarget in GetAllAssignmentTargets(target))
            {
                if (IsPrimaryConstructorParameter(innerTarget, context.CancellationToken))
                {
                    context.ReportDiagnostic(s_rule, innerTarget);
                }
            }
        }
        else if (IsPrimaryConstructorParameter(target, context.CancellationToken))
        {
            context.ReportDiagnostic(s_rule, target);
        }
    }

    private static List<IOperation> GetAllAssignmentTargets(IOperation operation)
    {
        var result = new List<IOperation>();
        GetAllAssignmentTargets(result, operation);
        return result;

        static void GetAllAssignmentTargets(List<IOperation> operations, IOperation operation)
        {
            if (operation is ITupleOperation tuple)
            {
                foreach (var element in tuple.Elements)
                {
                    GetAllAssignmentTargets(operations, element);
                }
            }
            else
            {
                operations.Add(operation);
            }
        }
    }

    private static bool IsPrimaryConstructorParameter(IOperation operation, CancellationToken cancellationToken)
    {
        if (operation is IParameterReferenceOperation parameterReferenceOperation)
        {
            if (parameterReferenceOperation.Parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor)
            {
                foreach (var syntaxRef in ctor.DeclaringSyntaxReferences)
                {
                    var syntax = syntaxRef.GetSyntax(cancellationToken);
                    if (syntax is ClassDeclarationSyntax or StructDeclarationSyntax)
                        return true;
                }
            }
        }

        return false;
    }
}
#endif
