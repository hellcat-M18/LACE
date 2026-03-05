using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lace.Runtime
{
    /// <summary>
    /// 論理条件式の再帰構造。
    /// パラメータの状態を AND/OR/NOT で組み合わせて評価する。
    /// </summary>
    [Serializable]
    public class Condition
    {
        /// <summary>条件の種類</summary>
        public ConditionType type = ConditionType.Param;

        /// <summary>Param の場合: 参照するパラメータ名</summary>
        public string parameterName;

        /// <summary>Param の場合: 期待する値（true = ON）</summary>
        public bool expectedValue = true;

        /// <summary>AND/OR/NOT の場合: 子条件のリスト</summary>
        public List<Condition> children = new List<Condition>();

        /// <summary>リーフノード（パラメータ参照）を作成</summary>
        public static Condition Param(string paramName, bool expected = true)
        {
            return new Condition
            {
                type = ConditionType.Param,
                parameterName = paramName,
                expectedValue = expected,
            };
        }

        /// <summary>AND 条件を作成</summary>
        public static Condition And(params Condition[] children)
        {
            return new Condition
            {
                type = ConditionType.AND,
                children = new List<Condition>(children),
            };
        }

        /// <summary>OR 条件を作成</summary>
        public static Condition Or(params Condition[] children)
        {
            return new Condition
            {
                type = ConditionType.OR,
                children = new List<Condition>(children),
            };
        }

        /// <summary>NOT 条件を作成</summary>
        public static Condition Not(Condition child)
        {
            return new Condition
            {
                type = ConditionType.NOT,
                children = new List<Condition> { child },
            };
        }
    }

    public enum ConditionType
    {
        /// <summary>パラメータの値を直接参照</summary>
        Param = 0,

        /// <summary>全ての子条件が true の場合に true</summary>
        AND = 1,

        /// <summary>いずれかの子条件が true の場合に true</summary>
        OR = 2,

        /// <summary>子条件（1つ）の否定</summary>
        NOT = 3,
    }
}
