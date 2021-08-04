using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PatternMatchingRefactoringProvider
{
    /// <summary>
    /// Transforms a variable declaration into a pattern matching expression. Sadly without declaration at the moment.
    /// </summary>
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(PatternMatchingRefactoringProviderCodeRefactoringProvider)), Shared]
    internal class PatternMatchingRefactoringProviderCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            if (GetCreateDokumentTaskCreator(context, node) is Func<CancellationToken, Task<Document>> createChangedDocument)
            {
                CodeAction action = CodeAction.Create("Pattern Match", createChangedDocument);
                // Register this code action.
                context.RegisterRefactoring(action);
            }

            return;
        }


        /// <summary>
        /// Transforms a variable declaration into a pattern matching expression. Sadly without declaration at the moment.
        /// </summary>
        private async Task<Document> MakePatternMatchingClause(Document document, LocalDeclarationStatementSyntax localDeclartionSyntax, CancellationToken c)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (document.TryGetSyntaxRoot(out SyntaxNode root))
                    {
                        SyntaxEditor editor = new SyntaxEditor(root, document.Project.Solution.Workspace);
                        VariableDeclarationSyntax declaration = localDeclartionSyntax.Declaration;
                        VariableDeclaratorSyntax variableDeclarationSyntax = declaration.Variables.FirstOrDefault();

                        TypeSyntax type = declaration.Type; //The type used for the pattern matching.
                        ExpressionSyntax method = variableDeclarationSyntax.Initializer.Value; //The method used as the expression for the pattern matching
                        SyntaxToken identifier = variableDeclarationSyntax.Identifier; //Identifier I want to use as a declaration but cannot get to work

                        //I needed to use the older SyntaxFactory to get a pattern matching with declaration to emit. The newer SyntaxGenerator would only let me check the type
                        SingleVariableDesignationSyntax singleVariableDesignation = SyntaxFactory.SingleVariableDesignation(identifier); //First we create a single variable designation
                        DeclarationPatternSyntax singleVariableDeclaration = SyntaxFactory.DeclarationPattern(type, singleVariableDesignation); //We wrap in into a declaration
                        IsPatternExpressionSyntax isPatternDeclaration = SyntaxFactory.IsPatternExpression(method, singleVariableDeclaration); //The is pattern accepts the method and the declaration
                        IfStatementSyntax ifClause = SyntaxFactory.IfStatement(isPatternDeclaration, SyntaxFactory.Block()); //Finally we wrap it into a if clause with an empty block at the end.

                        editor.ReplaceNode(localDeclartionSyntax, ifClause); //Replace the found variable declaration with the new pattern matching expression.

                        return document.WithSyntaxRoot(editor.GetChangedRoot());
                    }
                }
                catch (Exception ex)
                {
                    //For Debugging.
                }
                return document;
            })
            .ConfigureAwait(false);
        }

        #region Wrappers and WrapperSelector


        private Func<CancellationToken, Task<Document>> GetCreateDokumentTaskCreator(CodeRefactoringContext context, SyntaxNode node)
        {
            Func<CancellationToken, Task<Document>> createChangedDocument = null;
            if (node is VariableDeclaratorSyntax variableDeclaratorSyntax)
                createChangedDocument = (c) => MakePatternMatchingClause(context.Document, variableDeclaratorSyntax, c);
            else if (node is VariableDeclarationSyntax variableDeclarationSyntax)
                createChangedDocument = (c) => MakePatternMatchingClause(context.Document, variableDeclarationSyntax, c);
            else if (node is LocalDeclarationStatementSyntax localDeclartionSyntax)
                createChangedDocument = (c) => MakePatternMatchingClause(context.Document, localDeclartionSyntax, c);

            return createChangedDocument;
        }

        private async Task<Document> MakePatternMatchingClause(Document document, VariableDeclaratorSyntax variableDeclaratorSyntax, CancellationToken c)
        {
            if (variableDeclaratorSyntax.Parent is VariableDeclarationSyntax variableDeclarationSyntax)
                if (variableDeclarationSyntax.Parent is LocalDeclarationStatementSyntax localDeclarationStatementSyntax)
                    return await MakePatternMatchingClause(document, localDeclarationStatementSyntax, c);
            return document;
        }

        private async Task<Document> MakePatternMatchingClause(Document document, VariableDeclarationSyntax variableDeclarationSyntax, CancellationToken c)
        {
            if (variableDeclarationSyntax.Parent is LocalDeclarationStatementSyntax localDeclarationStatementSyntax)
                return await MakePatternMatchingClause(document, localDeclarationStatementSyntax, c);
            return document;
        }

        #endregion
    }
}
