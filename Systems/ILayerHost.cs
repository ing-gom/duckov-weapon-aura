using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 레이어 목록을 가진 프로필.
    ///
    /// 오라·총구 화염·근접 참격이 각자 자기 레이어를 갖습니다. 편집 화면은 하나뿐이라,
    /// "지금 무엇의 레이어를 만지는가"만 이 인터페이스로 갈아 끼웁니다 — 탭마다 같은
    /// 편집기를 세 벌 만들면 한쪽만 고치는 순간 셋이 서로 달라집니다.
    ///
    /// 잔상은 뺐습니다. 붙는 대상이 <b>날아가는 총알 하나하나</b>라 연사하면 이미터가
    /// 순식간에 수십 개가 되고, 수명·상한 관리가 다른 셋과 완전히 다릅니다.
    /// </summary>
    public interface ILayerHost
    {
        WeaponEffectLayer[] Layers { get; }

        /// <summary>레이어를 하나 더합니다. 상한에 닿으면 null.</summary>
        WeaponEffectLayer? AddLayer();

        bool RemoveLayer(int index);

        WeaponEffectLayer? GetLayer(int index);

        int LayerLimit { get; }

        /// <summary>기본 색 — 새 레이어가 물려받습니다.</summary>
        Color LayerSeedColor { get; }
    }

    /// <summary>
    /// 레이어 목록을 다루는 공통 동작.
    ///
    /// 세 프로필이 똑같은 배열 조작을 갖게 되므로 한곳에 둡니다. 프로필마다 배열 필드를
    /// 직접 들고 있어야 해서(직렬화 때문에) 상속 대신 도우미로 뺐습니다.
    /// </summary>
    public static class LayerList
    {
        public const int Max = 4;

        public static WeaponEffectLayer? Add(ref WeaponEffectLayer[] layers, Color seed)
        {
            if (layers == null)
                layers = Array.Empty<WeaponEffectLayer>();

            if (layers.Length >= Max)
                return null;

            // 마지막 것을 복사해서 시작합니다 — 빈 값에서 출발하면 추가하자마자
            // 아무것도 안 보여서 "추가가 안 됐다"로 읽힙니다.
            var created = layers.Length > 0
                ? layers[layers.Length - 1].Clone()
                : WeaponEffectLayer.CreateDefault(seed);

            created.name = $"Layer {layers.Length + 1}";

            var list = new List<WeaponEffectLayer>(layers) { created };
            layers = list.ToArray();
            return created;
        }

        public static bool Remove(ref WeaponEffectLayer[] layers, int index)
        {
            if (layers == null || index < 0 || index >= layers.Length)
                return false;

            var list = new List<WeaponEffectLayer>(layers);
            list.RemoveAt(index);
            layers = list.ToArray();
            return true;
        }

        public static WeaponEffectLayer? Get(WeaponEffectLayer[]? layers, int index)
        {
            if (layers == null || index < 0 || index >= layers.Length)
                return null;

            return layers[index];
        }

        /// <summary>깊은 복사 — 프로필 복사에서 레이어가 딸려 가야 합니다.</summary>
        public static WeaponEffectLayer[] Clone(WeaponEffectLayer[]? source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<WeaponEffectLayer>();

            var copied = new WeaponEffectLayer[source.Length];
            for (int i = 0; i < copied.Length; i++)
                copied[i] = source[i]?.Clone() ?? new WeaponEffectLayer();

            return copied;
        }
    }
}
