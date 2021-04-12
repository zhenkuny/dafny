using System;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny.Linear
{
    internal class PreservedContents
    {
        public readonly String Atomic;
        public readonly String NewValue;

        public PreservedContents(String atomic, String newValue)
        {
            this.Atomic = atomic;
            this.NewValue = newValue;
        }
    }
    
    public class AtomicRewriter : IRewriter
    {
        
        internal override void PostResolve(ModuleDefinition module)
        {
            foreach (var (_, method) in Visit.AllMethodMembers(module))
            {
                RewriteMethod(method);
            }
        }

        private const String EXECUTE_PREFIX = "execute__atomic__";
        private const String FINISH_NAME = "finish__atomic";

        private bool is_open_stmt(CallStmt call)
        {
            var name = call.Method.CompileName;
            var parts = name.Split(".");
            return parts[^1].StartsWith(EXECUTE_PREFIX);
        }

        private bool is_close_stmt(CallStmt call)
        {
            var name = call.Method.CompileName;
            var parts = name.Split(".");
            return parts[^1] == FINISH_NAME;
        }

        /*private void check_names_match(IToken tok, String s1, String s2)
        {
            var parts = s1.Split(".");
            Contract.Assert(parts[^1].StartsWith(EXECUTE_PREFIX));
            var newName = parts[^1].Substring(EXECUTE_PREFIX.Length);
            parts[^1] = FINISH_PREFIX + newName;
            var expectedCloseName = String.Join(".", parts);
            
            if (expectedCloseName != s2)
            {
                reporter.Error(MessageSource.Rewriter, tok,
                    "Improper atomic statement nesting: close and open don't match");
            }
        }*/

        private void check_open_close_match(
                    CallStmt openStmt,
                    CallStmt closeStmt,
                    PreservedContents openPreservedContents)
        {
            //var call1 = openStmt;
            //var call2 = closeStmt;
            //check_names_match(openStmt.Tok, call1.Method.CompileName, call2.Method.CompileName);

            PreservedContents closedPreservedContents = get_preserved_contents_close_stmt(closeStmt);

            if (openPreservedContents.Atomic != null
                && closedPreservedContents.Atomic != null
                && openPreservedContents.Atomic != closedPreservedContents.Atomic)
            {
                reporter.Error(MessageSource.Rewriter, closeStmt.Tok,
                    "Improper atomic statement nesting: 'atomic' field does not match: got '{0}' and '{1}'",
                    openPreservedContents.Atomic,
                    closedPreservedContents.Atomic);
            }
            
            if (openPreservedContents.NewValue != null
                && closedPreservedContents.NewValue != null
                && openPreservedContents.NewValue != closedPreservedContents.NewValue)
            {
                reporter.Error(MessageSource.Rewriter, closeStmt.Tok,
                    "Improper atomic statement nesting: 'new_value' field does not match: got '{0}' and '{1}'",
                    openPreservedContents.NewValue,
                    closedPreservedContents.NewValue);
            }
        }

        private String call_get_arg(CallStmt call, int i)
        {
            if (i >= call.Args.Count)
            {
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get arg of stmt: not enough args");
                return null;
            }
            else
            {
                var arg = call.Args[i].Expr;
                if (arg is NameSegment ns)
                {
                    if (ns.Resolved is IdentifierExpr id)
                    {
                        return id.Var.CompileName;
                    }
                }
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get arg of stmt: not an IdentifierExpr");
                return null;
            }
        }
        
        private String call_get_out_arg(CallStmt call, int i)
        {
            if (i >= call.Lhs.Count)
            {
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get out arg of stmt: not enough args");
                return null;
            }
            else
            {
                var arg = call.Lhs[i];
                if (arg is IdentifierExpr id)
                {
                    return id.Var.CompileName;
                }
                else
                {
                    reporter.Error(MessageSource.Rewriter, call.Tok,
                        "Atomic checking: can't get out arg of stmt: not an IdentifierExpr");
                    return null;
                }
            }
        }

        private PreservedContents get_preserved_contents_open_stmt(CallStmt openStmt)
        {
            return new PreservedContents(
                call_get_arg(openStmt, 0),
                call_get_out_arg(openStmt, 2));
        }
        
        private PreservedContents get_preserved_contents_close_stmt(CallStmt closeStmt)
        {
            return new PreservedContents(
                call_get_arg(closeStmt, 0),
                call_get_arg(closeStmt, 1));
        }

        private static CallStmt as_call_stmt(Statement stmt)
        {
            if (stmt is VarDeclStmt vds)
            {
                var cus = vds.Update;
                if (cus is UpdateStmt us)
                {
                    if (us.SubStatements.Count() == 1)
                    {
                        var sub = us.SubStatements.First();
                        if (sub is CallStmt cs)
                        {
                            return cs;
                        }
                    }
                }
            } else if (stmt is UpdateStmt us)
            {
                if (us.SubStatements.Count() == 1)
                {
                    var sub = us.SubStatements.First();
                    if (sub is CallStmt cs)
                    {
                        return cs;
                    }
                }
            } else if (stmt is CallStmt cs)
            {
                return cs;
            }

            return null;
        }

        private void check_no_assign_lhs(Expression lhs, String varName)
        {
            switch (lhs)
            {
                case NameSegment ns:
                    check_no_assign_lhs(ns.Resolved, varName);
                    break;
                case IdentifierExpr ie:
                    if (ie.Var.CompileName == varName)
                    {
                        reporter.Error(MessageSource.Rewriter, lhs.tok,
                            "Assign to variable which should be preserved for atomic block");
                    }

                    break;
                default:
                    Contract.Assert(false);
                    break;
            }
        }

        private void check_no_assign_stmt(Statement stmt, String varName)
        {
            if (stmt is UpdateStmt us)
            {
                foreach (Expression lhs in us.Lhss)
                {
                    check_no_assign_lhs(lhs, varName);
                }
            }
        }

        private void check_no_assign(Statement stmt, String varName)
        {
            check_no_assign_stmt(stmt, varName);
            foreach (var stmtList in Visit.AllStatementLists(stmt))
            {
                foreach (var s in stmtList)
                {
                    check_no_assign_stmt(s, varName);
                }
            }
        }

        public static bool IsGlinearStmt(Statement stmt)
        {
            var callStmt = as_call_stmt(stmt);
            if (callStmt != null)
            {
                var u = callStmt.Method.Usage;
                return ((u.IsLinearKind || u.IsSharedKind) && u.realm == LinearRealm.Erased && callStmt.Method.IsStatic);
            }

            if (stmt is VarDeclStmt vds)
            {
                return IsGlinearStmt(vds.Update);
            } else if (stmt is UpdateStmt us)
            {
                foreach (Statement sub in us.SubStatements)
                {
                    if (!IsGlinearStmt(sub))
                    {
                        return false;
                    }
                }

                return true;
            } else if (stmt is AssignStmt ast)
            {
                if (ast.Lhs is IdentifierExpr ie)
                {
                    var u = ie.Var.Usage;
                    return (u.IsLinearKind || u.IsSharedKind) && u.realm == LinearRealm.Erased;
                }

                return false;
            }

            return false;
        }
        
        private void RewriteMethod(Method m) {
            var body = m.Body?.Body;
            if (body == null)
            {
                return;
            }
            foreach (var stmtList in Visit.AllStatementLists(body))
            {
                CallStmt openStmt = null;
                PreservedContents preservedContents = null;
                foreach (var stmt in stmtList)
                {
                    CallStmt call = as_call_stmt(stmt);
                    if (call != null && is_open_stmt(call))
                    {
                        if (openStmt != null)
                        {
                            reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                "Improper atomic statement nesting: double-open");
                        }

                        openStmt = call;
                        preservedContents = get_preserved_contents_open_stmt(call);
                        
                        check_no_assign(openStmt, preservedContents.Atomic);
                    } else if (call != null && is_close_stmt(call))
                    {
                        if (openStmt == null)
                        {
                            reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                "Improper atomic statement nesting: close without corresponding open");
                        }
                        else
                        {
                            check_open_close_match(openStmt, call, preservedContents);
                            openStmt = null;
                            preservedContents = null;
                        }
                    }
                    else
                    {
                        if (openStmt != null)
                        {
                            if (!stmt.IsGhost && !IsGlinearStmt(stmt))
                            {
                                reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                    "Only ghost statements can be within an atomic block");
                            }
                            
                            check_no_assign(stmt, preservedContents.Atomic);
                            check_no_assign(stmt, preservedContents.NewValue);
                        }
                    }
                }

                if (openStmt != null)
                {
                    reporter.Error(MessageSource.Rewriter, m.Body.Tok,
                        "block ends with an atomic block open");
                }
            }
        }

        public AtomicRewriter(ErrorReporter reporter) : base(reporter)
        {
        }
    }
}