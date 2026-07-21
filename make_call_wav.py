# -*- coding: utf-8 -*-
"""
エンディングの「おーい！」呼びかけ音を生成するスクリプト（標準ライブラリのみ）

clear.wav（クリア音）と混同しないよう、あえて別の音色にしてある:
  ・clear 系は「和音・派手・下降」になりがちなので、こちらは
    「2音節・単音的・上昇（呼びかけの抑揚）」で作る
  ・語尾を上げることで「呼びかけ／問いかけ」に聞こえる

出力: Assets/Audio/call.wav （16bit / 44.1kHz / モノラル）

実行:  python make_call_wav.py
音を変えたいときは下の CONFIG をいじって再実行する。
"""
import math
import os
import random
import struct
import wave

# ================= CONFIG =================
OUT_PATH = os.path.join("Assets", "Audio", "call.wav")
RATE = 44100
PEAK = 0.82          # 最終音量（1.0でクリップ手前）

# 2音節の「おー」「いー」。(開始周波数, 終了周波数, 長さ秒)
SYLLABLES = [
    (600.0, 560.0, 0.22),   # 「おー」少し下がる
    (740.0, 920.0, 0.32),   # 「いー」上がって呼びかけの抑揚になる
]
GAP = 0.05           # 音節の間(秒)

VIBRATO_HZ = 5.5     # 声の揺れ
VIBRATO_DEPTH = 0.018
BREATH = 0.035       # 息の成分（多いとかすれる）

# 倍音の混ぜ具合。多いほど「ぶーぶー」した動物っぽい声になる
HARMONICS = [(1, 1.00), (2, 0.42), (3, 0.20), (4, 0.09)]
# ==========================================


def envelope(pos, length):
    """立ち上がり・減衰。呼び声なので立ち上がりは速め、余韻は短め"""
    attack = 0.018
    release = 0.075
    if pos < attack:
        return pos / attack
    if pos > length - release:
        return max(0.0, (length - pos) / release)
    # 発声中はわずかに減衰させると生っぽくなる
    return 1.0 - 0.15 * ((pos - attack) / max(1e-6, length - attack - release))


def build():
    random.seed(7)
    samples = []
    phase = 0.0

    for idx, (f_start, f_end, dur) in enumerate(SYLLABLES):
        n = int(RATE * dur)
        for i in range(n):
            pos = i / RATE
            k = i / max(1, n - 1)

            # 周波数は音節内でなめらかに変化させる（抑揚）
            f = f_start + (f_end - f_start) * (k ** 0.8)
            f *= 1.0 + math.sin(2 * math.pi * VIBRATO_HZ * pos) * VIBRATO_DEPTH

            phase += 2 * math.pi * f / RATE

            v = 0.0
            for mult, amp in HARMONICS:
                v += math.sin(phase * mult) * amp
            v /= sum(a for _, a in HARMONICS)

            v += (random.random() * 2 - 1) * BREATH        # 息づかい
            v *= envelope(pos, dur)
            samples.append(v)

        # 音節のあいだの無音
        if idx < len(SYLLABLES) - 1:
            samples.extend([0.0] * int(RATE * GAP))

    # 正規化してクリップを防ぐ
    peak = max(abs(s) for s in samples) or 1.0
    scale = PEAK / peak
    return [int(max(-1.0, min(1.0, s * scale)) * 32767) for s in samples]


def main():
    data = build()
    os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
    with wave.open(OUT_PATH, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", s) for s in data))

    print(f"出力しました: {OUT_PATH}")
    print(f"  長さ {len(data) / RATE:.2f} 秒 / {RATE}Hz / 16bit モノラル")


if __name__ == "__main__":
    main()
