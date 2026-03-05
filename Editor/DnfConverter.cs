using System;
using System.Collections.Generic;
using System.Linq;
using Lace.Runtime;

namespace Lace.Editor
{
    /// <summary>
    /// 条件式を DNF（加法標準形: OR of ANDs）に正規化するユーティリティ。
    /// </summary>
    public static class DnfConverter
    {
        /// <summary>
        /// リテラル（Param の正・負）を表す。
        /// </summary>
        public struct Literal
        {
            public string ParameterName;
            public bool IsPositive; // true = Param==ON, false = Param==OFF
        }

        /// <summary>
        /// AND 節（リテラルの集合）。
        /// </summary>
        public class Conjunction
        {
            public List<Literal> Literals = new List<Literal>();
        }

        /// <summary>
        /// DNF 全体（AND 節の OR）。
        /// </summary>
        public class Dnf
        {
            public List<Conjunction> Conjunctions = new List<Conjunction>();
        }

        /// <summary>
        /// Condition ツリーを DNF に変換する。
        /// 1. NOT をド・モルガンの法則で内側に押し込む
        /// 2. AND / OR を分配法則で展開し、OR of ANDs に正規化
        /// </summary>
        public static Dnf ToDnf(Condition condition)
        {
            var normalized = EliminateNot(condition, false);
            return ConvertToDnf(normalized);
        }

        /// <summary>
        /// 条件式を否定する（ド・モルガンの法則）。
        /// </summary>
        public static Condition Negate(Condition condition)
        {
            switch (condition.type)
            {
                case ConditionType.Param:
                    return Condition.Param(condition.parameterName, !condition.expectedValue);
                case ConditionType.AND:
                    return Condition.Or(condition.children.Select(Negate).ToArray());
                case ConditionType.OR:
                    return Condition.And(condition.children.Select(Negate).ToArray());
                case ConditionType.NOT:
                    return condition.children[0]; // 二重否定の除去
                default:
                    throw new InvalidOperationException($"Unknown ConditionType: {condition.type}");
            }
        }

        /// <summary>
        /// NOT を内側に押し込む。negated が true の場合、条件全体が否定されている。
        /// 結果は Param / AND / OR のみで構成される（NOT は消える）。
        /// </summary>
        private static Condition EliminateNot(Condition c, bool negated)
        {
            switch (c.type)
            {
                case ConditionType.Param:
                    return new Condition
                    {
                        type = ConditionType.Param,
                        parameterName = c.parameterName,
                        expectedValue = negated ? !c.expectedValue : c.expectedValue,
                    };

                case ConditionType.NOT:
                    if (c.children == null || c.children.Count == 0)
                        throw new InvalidOperationException("NOT node must have exactly one child");
                    return EliminateNot(c.children[0], !negated);

                case ConditionType.AND:
                    if (negated)
                    {
                        // NOT(AND(a,b)) = OR(NOT(a), NOT(b))
                        return new Condition
                        {
                            type = ConditionType.OR,
                            children = c.children.Select(ch => EliminateNot(ch, true)).ToList(),
                        };
                    }
                    return new Condition
                    {
                        type = ConditionType.AND,
                        children = c.children.Select(ch => EliminateNot(ch, false)).ToList(),
                    };

                case ConditionType.OR:
                    if (negated)
                    {
                        // NOT(OR(a,b)) = AND(NOT(a), NOT(b))
                        return new Condition
                        {
                            type = ConditionType.AND,
                            children = c.children.Select(ch => EliminateNot(ch, true)).ToList(),
                        };
                    }
                    return new Condition
                    {
                        type = ConditionType.OR,
                        children = c.children.Select(ch => EliminateNot(ch, false)).ToList(),
                    };

                default:
                    throw new InvalidOperationException($"Unknown ConditionType: {c.type}");
            }
        }

        /// <summary>
        /// NOT 除去済みの条件式（Param / AND / OR のみ）を DNF に変換する。
        /// - Param → リテラル1つの AND 節1つ
        /// - OR → 各子の DNF を結合（和集合）
        /// - AND → 各子の DNF の直積（分配法則）
        /// </summary>
        private static Dnf ConvertToDnf(Condition c)
        {
            switch (c.type)
            {
                case ConditionType.Param:
                {
                    var conj = new Conjunction();
                    conj.Literals.Add(new Literal
                    {
                        ParameterName = c.parameterName,
                        IsPositive = c.expectedValue,
                    });
                    return new Dnf { Conjunctions = new List<Conjunction> { conj } };
                }

                case ConditionType.OR:
                {
                    var result = new Dnf();
                    foreach (var child in c.children)
                    {
                        var childDnf = ConvertToDnf(child);
                        result.Conjunctions.AddRange(childDnf.Conjunctions);
                    }
                    return result;
                }

                case ConditionType.AND:
                {
                    // 空の AND 節1つから開始し、各子の DNF と直積をとる
                    var result = new Dnf();
                    result.Conjunctions.Add(new Conjunction());

                    foreach (var child in c.children)
                    {
                        var childDnf = ConvertToDnf(child);
                        var expanded = new List<Conjunction>();

                        foreach (var existing in result.Conjunctions)
                        {
                            foreach (var childConj in childDnf.Conjunctions)
                            {
                                var merged = new Conjunction();
                                merged.Literals.AddRange(existing.Literals);
                                merged.Literals.AddRange(childConj.Literals);
                                expanded.Add(merged);
                            }
                        }
                        result.Conjunctions = expanded;
                    }
                    return result;
                }

                case ConditionType.NOT:
                    throw new InvalidOperationException(
                        "NOT should have been eliminated before DNF conversion");

                default:
                    throw new InvalidOperationException($"Unknown ConditionType: {c.type}");
            }
        }
    }
}
