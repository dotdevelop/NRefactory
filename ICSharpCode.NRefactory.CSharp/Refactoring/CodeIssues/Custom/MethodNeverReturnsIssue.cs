﻿// 
// MethodNeverReturnsIssue.cs
// 
// Author:
//      Mansheng Yang <lightyang0@gmail.com>
// 
// Copyright (c) 2012 Mansheng Yang <lightyang0@gmail.com>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections.Generic;
using ICSharpCode.NRefactory.CSharp.Analysis;
using ICSharpCode.NRefactory.Refactoring;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using System.Runtime.InteropServices.ComTypes;

namespace ICSharpCode.NRefactory.CSharp.Refactoring
{
	[IssueDescription ("Method never returns",
		Description = "Method does not reach its end or a 'return' statement by any of possible execution paths.",
		Category = IssueCategories.CodeQualityIssues,
		Severity = Severity.Warning,
		IssueMarker = IssueMarker.WavedLine)]
	public class MethodNeverReturnsIssue : GatherVisitorCodeIssueProvider
	{
		protected override IGatherVisitor CreateVisitor(BaseRefactoringContext context)
		{
			return new GatherVisitor(context);
		}

		class GatherVisitor : GatherVisitorBase<MethodNeverReturnsIssue>
		{
			public GatherVisitor(BaseRefactoringContext ctx)
				: base (ctx)
			{
			}

			public override void VisitMethodDeclaration (MethodDeclaration methodDeclaration)
			{
				var body = methodDeclaration.Body;

				// partial method
				if (body.IsNull)
					return;

				var memberResolveResult = ctx.Resolve(methodDeclaration) as MemberResolveResult;
				VisitBody("Method", methodDeclaration.NameToken, body, memberResolveResult == null ? null : memberResolveResult.Member);

				base.VisitMethodDeclaration (methodDeclaration);
			}

			public override void VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
			{
				VisitBody("Delegate", anonymousMethodExpression.DelegateToken, anonymousMethodExpression.Body, null);

				base.VisitAnonymousMethodExpression(anonymousMethodExpression);
			}

			public override void VisitAccessor(Accessor accessor)
			{
				var memberResolveResult = ctx.Resolve(accessor) as MemberResolveResult;
				VisitBody("Accessor", accessor.Keyword, accessor.Body, memberResolveResult == null ? null : memberResolveResult.Member);

				base.VisitAccessor (accessor);
			}

			public override void VisitLambdaExpression(LambdaExpression lambdaExpression)
			{
				var body = lambdaExpression.Body as BlockStatement;
				if (body != null) {
					VisitBody("Lambda expression", lambdaExpression.ArrowToken, body, null);
				}

				//Even if it is an expression, we still need to check for children
				//for cases like () => () => { while (true) {}}
				base.VisitLambdaExpression(lambdaExpression);
			}

			void VisitBody(string entityType, AstNode node, BlockStatement body, IMember member)
			{
				var reachability = ctx.CreateReachabilityAnalysis(body, new RecursiveDetector(ctx, member));
				bool hasReachableReturn = false;
				foreach (var statement in reachability.ReachableStatements) {
					if (statement is ReturnStatement || statement is ThrowStatement || statement is YieldBreakStatement) {
						hasReachableReturn = true;
						break;
					}
				}
				if (!hasReachableReturn && !reachability.IsEndpointReachable(body)) {
					AddIssue(node, ctx.TranslateString(string.Format("{0} never reaches its end or a 'return' statement.", entityType)));
				}
			}

			class RecursiveDetector : ReachabilityAnalysis.RecursiveDetectorVisitor
			{
				BaseRefactoringContext ctx;
				IMember member;

				internal RecursiveDetector(BaseRefactoringContext ctx, IMember member) {
					this.ctx = ctx;
					this.member = member;
				}

				public override void VisitIdentifierExpression(IdentifierExpression identifierExpression)
				{
					CheckRecursion(identifierExpression);
				}

				public override void VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
				{
					base.VisitMemberReferenceExpression(memberReferenceExpression);

					if (!CurrentResult) {
						PopResult();
						CheckRecursion(memberReferenceExpression);
					}
				}

				public override void VisitInvocationExpression(InvocationExpression invocationExpression)
				{
					base.VisitInvocationExpression(invocationExpression);

					if (!CurrentResult) {
						PopResult();
						CheckRecursion(invocationExpression);
					}
				}

				void CheckRecursion(AstNode node) {
					if (member == null) {
						PushResult(false);
						return;
					}

					var resolveResult = ctx.Resolve(node);

					//We'll ignore Method groups here
					//If the invocation expressions will be dealt with later anyway
					//and properties are never in "method groups".
					var memberResolveResult = resolveResult as MemberResolveResult;
					if (memberResolveResult == null || memberResolveResult.Member != this.member) {
						PushResult(false);
						return;
					}

					PushResult(true);
				}
			}
		}
	}
}
