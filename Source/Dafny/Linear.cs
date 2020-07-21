using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using System.Diagnostics.Contracts;
using System.ComponentModel;

namespace Microsoft.Dafny.Linear {

  // TODO(andrea) move/remove
  static class Util {

    internal static void OxideDebug(IToken token, String msg, params object[] args) {
      if (DafnyOptions.O.OxideDebug) {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Error.WriteLine("[oxide] " + ErrorReporter.TokenToString(token).PadRight(20) + " " + String.Format(msg, args));
        Console.ResetColor();
      }
    }

    static public void PrintObject(Object obj) {
        System.Type t = obj.GetType();
        foreach(var fieldInfo in t.GetFields()) {
            Console.WriteLine("[f] {0}={1}", fieldInfo.Name, fieldInfo.GetValue(obj));
        }
        foreach(PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
        {
            string name=descriptor.Name;
            object value;
            try {
                value = descriptor.GetValue(obj);
            } catch (System.Reflection.TargetInvocationException e) {
                if (e.InnerException is NullReferenceException) {
                  value = "<NullReferenceException>";
                } else {
                  value = "<?Exception?>";
                }
            }
            Console.WriteLine("[p] {0}={1}",name,value);
        }
    }

    static public void PrintObjects(Object obj1, Object obj2) {
        System.Type t = obj1.GetType();
        foreach(var fieldInfo in t.GetFields()) {
            var value1 = fieldInfo.GetValue(obj1);
            var value2 = fieldInfo.GetValue(obj2);
            Console.WriteLine("[f] {3} {0} = {1} <> {2}", fieldInfo.Name,
                value1, value2, value1 != null ? (value1.Equals(value2) ? "  " : "!=") : "??");
        }
        foreach(PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj1))
        {
            string name=descriptor.Name;
            object value1;
            try {
                value1 = descriptor.GetValue(obj1);
            } catch (System.Reflection.TargetInvocationException e) {
                if (e.InnerException is NullReferenceException) {
                  value1 = "<NullReferenceException>";
                } else {
                  value1 = "<?Exception?>";
                }
            }
            object value2;
            try {
                value2 = descriptor.GetValue(obj2);
            } catch (System.Reflection.TargetInvocationException e) {
                if (e.InnerException is NullReferenceException) {
                  value2 = "<NullReferenceException>";
                } else {
                  value2 = "<?Exception?>";
                }
            }
            Console.WriteLine("[p] {3} {0} = {1} <> {2}", name, value1, value2, value1.Equals(value2) ? "  " : "!=");
        }
    }
  }

  public class InoutTranslateRewriter : IRewriter {
    public delegate string FreshTempVarName(string prefix, ICodeContext context);

    FreshTempVarName freshTempVarName;
    
    public InoutTranslateRewriter(ErrorReporter reporter, FreshTempVarName freshTempVarName) : base(reporter) {
      this.freshTempVarName = freshTempVarName;
    }

    Cloner cloner = new Cloner();

    static IEnumerable<List<Statement>> AllStatementLists(List<Statement> stmts) {
      foreach (var stmt in stmts) {
        switch (stmt) {
          case BlockStmt bs:
            foreach (var ls in AllStatementLists(bs.Body)) { yield return ls; }
            break;
          case IfStmt ifs:
            foreach (var ls in AllStatementLists(ifs.Thn.Body)) { yield return ls; }
            if (ifs.Els != null) {
              foreach (var ls in AllStatementLists(((BlockStmt) ifs.Els).Body)) { yield return ls; }
            }
            break;
          case NestedMatchStmt ms:
            foreach (var mc in ms.Cases) {
              foreach (var ls in AllStatementLists(mc.Body)) { yield return ls; }
            }
            break;
          case WhileStmt ws:
            foreach (var ls in AllStatementLists(ws.Body.Body)) { yield return ls; }
            break;
        }
      }
      yield return stmts;
    }

    (Expression, Expression)? DatatypeUpdateExprFor(Expression expr, Expression value) {
      var tok = new AutoGeneratedToken(expr.tok);
      if (expr is ExprDotName) {
        var dotName = (ExprDotName) expr;
        var newValue = new DatatypeUpdateExpr(tok, cloner.CloneExpr(dotName.Lhs),
          new List<Tuple<IToken, string, Expression>> { Tuple.Create((IToken) tok, dotName.SuffixName, value) });
        return DatatypeUpdateExprFor(dotName.Lhs, newValue);
      } else if (expr is NameSegment) {
        return (new IdentifierExpr(tok, ((NameSegment) expr).Name), value);
      } else if (expr is ThisExpr) {
        return null; // (new ThisExpr(tok), value);
      } else {
        reporter.Error(MessageSource.Rewriter, expr.tok, "invalid inout argument");
        return null;
      }
    }

    void PreRewriteMethod(Method m, TopLevelDecl enclosingDecl) {
      Util.OxideDebug(m.tok, "Rewriting method {0}", m.Name);
      Contract.Requires(m != null);
      // TODO(andrea) m.CompileOuts = m.Outs.ToList();
      // TODO(andrea) m.CompileIns = m.Ins.ToList();

      var body = m.Body?.Body;

      void AddGeneratedInoutFormals(Formal f, int insertAt) {
        var fTok = new AutoGeneratedToken(f.tok);

        var outFormal = new Formal(fTok, f.Name, f.Type, false, f.Usage, false, false);
        m.Outs.Add(outFormal);

        var inFormal = new Formal(fTok, "old_" + f.Name, f.Type, false, f.Usage, false, true);
        m.Ins.Insert(insertAt, inFormal);

        if (body != null) {
          var mTok = new AutoGeneratedToken(m.tok);

          var lhs = new IdentifierExpr(mTok, outFormal);
          var rhsNameSegment = new NameSegment(fTok, "old_" + f.Name, null);
          var rhs = new ExprRhs(rhsNameSegment);
          var updateStmt = new UpdateStmt(mTok, mTok,
            new List<Expression> { lhs },
            new List<AssignmentRhs> { rhs });
          updateStmt.InoutGenerated = false;
          body.Insert(0, updateStmt);
        }
      }

      for (int i = 0; i < m.Ins.Count; i++) {
        if (m.Ins[i].Inout) {
          var f = m.Ins[i];
          m.Ins.RemoveAt(i);

          AddGeneratedInoutFormals(f, i);
        }
      }

      if (m.HasInoutThis) {
        AddGeneratedInoutFormals(new Formal(new AutoGeneratedToken(m.tok), "self", UserDefinedType.FromTopLevelDecl(m.tok, enclosingDecl), false, m.Usage, false, true), 0);
        // TODO(andrea) DISALLOW "this" in body
      }

      if (body != null) {
        foreach (var stmtList in AllStatementLists(body)) {
          for (int s = 0; s < stmtList.Count; s++) {
            var stmt = stmtList[s];
            var varDeclStmt = stmt as VarDeclStmt;
            var updStmt = (stmt as UpdateStmt) ?? (varDeclStmt?.Update as UpdateStmt);
            var firstExprRhs = updStmt?.Rhss.First() as ExprRhs;
            var applySuffix = firstExprRhs?.Expr as ApplySuffix;
            if (applySuffix != null) {
              var inoutArgs = applySuffix.Args.Where(a => a.Inout).ToList();
              if (inoutArgs.Count == 0 && !firstExprRhs.InoutThis) {
                continue;
              }

              // TODO(andrea)
              if (firstExprRhs.InoutThis) {
                Util.OxideDebug(stmt.Tok, "  rewriting " + Printer.StatementToString(stmt) + " (inout this)");
              } else {
                Util.OxideDebug(stmt.Tok, "  rewriting " + Printer.StatementToString(stmt));
              }

              if (firstExprRhs.InoutThis) {
                var dotNameMethod = applySuffix.Lhs as ExprDotName;
                if (dotNameMethod == null) {
                  reporter.Error(MessageSource.Rewriter, applySuffix, "invalid inout call");
                  return;
                }
                Expression selfArg = cloner.CloneExpr(((ExprDotName) applySuffix.Lhs).Lhs);
                var selfApplySuffixArg = new ApplySuffixArg { Expr = selfArg, Inout = true };
                applySuffix.Args.Insert(0, selfApplySuffixArg);
                inoutArgs.Add(selfApplySuffixArg);
              }

              var updatedFields = inoutArgs.ConvertAll(a => {
                var aTok = new AutoGeneratedToken(a.Expr.tok);
                var varName = freshTempVarName("_inout_tmp_", m);
                var datatypeUpdateFor = DatatypeUpdateExprFor(a.Expr, new IdentifierExpr(aTok, varName));
                // TODO(andrea)
                UpdateStmt updateStmt = null;
                if (datatypeUpdateFor.HasValue) {
                  var (updateLhs, updateExpr) = datatypeUpdateFor.Value;
                  updateStmt = new UpdateStmt(aTok, aTok,
                    new List<Expression> { updateLhs },
                    new List<AssignmentRhs> { new ExprRhs(updateExpr) });
                  updateStmt.InoutGenerated = true;
                  Util.OxideDebug(a.Expr.tok, "    varName: " + varName + ", " + Printer.ExprToString(a.Expr) + ", " + Printer.ExprToString(updateExpr));
                }
                return (
                  new LocalVariable(aTok, aTok, varName, new InferredTypeProxy(), Usage.Ignore),
                  updateStmt);
              });
              var tempLocalVars = updatedFields.Select(x => x.Item1).ToList();
              foreach (var tv in tempLocalVars) {
                updStmt.Lhss.Add(new IdentifierExpr(tv.Tok, tv.Name));
              }
              if (varDeclStmt == null) {
                var asTok = new AutoGeneratedToken(applySuffix.tok);
                varDeclStmt = new VarDeclStmt(asTok, asTok, tempLocalVars, null);
                stmtList.Insert(s, varDeclStmt);
                // TODO(andrea)
                Util.OxideDebug(stmtList[s].Tok, "    " + Printer.StatementToString(stmtList[s]));
                s++;
              } else {
                varDeclStmt.Locals.AddRange(tempLocalVars);
                // TODO(andrea)
                Util.OxideDebug(stmtList[s].Tok, "    " + Printer.StatementToString(stmtList[s]));
              }
              Util.OxideDebug(stmt.Tok, "    " + Printer.StatementToString(stmt));
              foreach (var newUpdStmt in updatedFields.Select(x => x.Item2)) {
                if (newUpdStmt != null) {
                  s++;
                  stmtList.Insert(s, newUpdStmt);
                  Util.OxideDebug(stmtList[s].Tok, "    " + Printer.StatementToString(stmtList[s]));
                }
              }
            } else if (firstExprRhs?.InoutThis ?? false) {
              reporter.Error(MessageSource.Rewriter, firstExprRhs.Tok, "inout not allowed here");
            }
          }
        }
      }
    }

    static bool memberIsMethod(MemberDecl decl) {
      var m = decl as Method;
      if (m == null) {
        return false;
      }
      return !(
          m is Constructor ||
          m is InductiveLemma ||
          m is CoLemma ||
          m is Lemma ||
          m is TwoStateLemma);
    }

    static IEnumerable<(TopLevelDecl, Method)> AllMethodMembers(ModuleDefinition module) {
      foreach (var decl in module.TopLevelDecls) {
        var topLevelDecl = ((decl as ClassDecl) as TopLevelDeclWithMembers) ?? ((decl as DatatypeDecl) as TopLevelDeclWithMembers);
        if (topLevelDecl != null) {
          foreach (var m in topLevelDecl.Members.Where(memberIsMethod)) {
            var method = (Method) m;
            yield return (topLevelDecl, method);
          }
        }
      }
    }

    internal override void PreResolve(ModuleDefinition module) {
      foreach (var (tld, method) in AllMethodMembers(module)) {
        PreRewriteMethod(method, tld);
      }
    }

    // TODO(andrea) remove
    // internal override void PostResolve(ModuleDefinition module) {
    //   foreach (var (tld, method) in AllMethodMembers(module)) {
    //   }
    // }

  }
}
