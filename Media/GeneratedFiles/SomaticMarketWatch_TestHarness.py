import json
import random
from dataclasses import dataclass
from typing import Dict, List


class Severity:
    WATCH = 1
    WARN = 2
    ACT = 3


class SignalClass:
    BREAKOUT = 0
    EXHAUSTION = 1
    REGIME = 2
    SENTIMENT = 3


@dataclass
class MarketEvent:
    symbol: str
    signal_class: int
    severity: int
    conviction: float
    timestamp_ms: int


@dataclass
class HapticPattern:
    rhythm: str
    intensity: int
    duration_ms: int
    location: int
    pattern_id: int


class HapticTranslator:
    # (signal_class, severity) -> (rhythm, intensity, duration_ms, location_mask, drv2605_waveform_id)
    PATTERNS = {
        (SignalClass.BREAKOUT, Severity.WATCH):   ('drum-roll', 40, 220, 0x01, 47),
        (SignalClass.BREAKOUT, Severity.WARN):    ('drum-roll', 75, 440, 0x01, 47),
        (SignalClass.BREAKOUT, Severity.ACT):     ('thunder', 118, 900, 0x01, 94),
        (SignalClass.EXHAUSTION, Severity.WATCH): ('heartbeat', 35, 320, 0x02, 27),
        (SignalClass.EXHAUSTION, Severity.WARN):  ('heartbeat', 72, 540, 0x02, 27),
        (SignalClass.EXHAUSTION, Severity.ACT):   ('siren', 122, 1100, 0x02, 58),
        (SignalClass.REGIME, Severity.WATCH):     ('pulse', 32, 260, 0x04, 10),
        (SignalClass.REGIME, Severity.WARN):      ('pulse', 68, 520, 0x04, 10),
        (SignalClass.REGIME, Severity.ACT):       ('alarm', 125, 1250, 0x04, 56),
        (SignalClass.SENTIMENT, Severity.WATCH):  ('tick', 28, 160, 0x08, 1),
        (SignalClass.SENTIMENT, Severity.WARN):   ('tick', 58, 340, 0x08, 1),
        (SignalClass.SENTIMENT, Severity.ACT):    ('buzz', 105, 760, 0x08, 52),
    }

    CLASS_NAMES = ['Breakout', 'Exhaustion', 'Regime', 'Sentiment']
    SEVERITY_NAMES = {1: 'Watch', 2: 'Warn', 3: 'Act'}
    LOCATION_NAMES = {0x01: 'wrist', 0x02: 'chest', 0x04: 'back', 0x08: 'ankle'}

    def translate(self, event: MarketEvent) -> HapticPattern:
        key = (event.signal_class, event.severity)
        if key not in self.PATTERNS:
            raise ValueError(f"Unknown mapping for class={event.signal_class}, severity={event.severity}")
        return HapticPattern(*self.PATTERNS[key])

    def to_frame(self, event: MarketEvent) -> bytes:
        hp = self.translate(event)
        payload = bytes([
            0xAA,                   # sync
            0x01,                   # version
            (event.signal_class << 4) | event.severity,
            hp.pattern_id,
            hp.intensity,
            min(hp.duration_ms // 10, 255),
            hp.location,
            0                       # checksum placeholder
        ])
        chk = sum(payload[:-1]) & 0xFF
        return payload[:-1] + bytes([chk])


def generate_synthetic_events(n: int = 1000, seed: int = 42) -> List[MarketEvent]:
    random.seed(seed)
    symbols = ['EURUSD', 'GBPUSD', 'USDJPY', 'AUDUSD', 'USDCAD', 'USDCHF', 'NZDUSD', 'EURGBP']
    events = []
    for i in range(n):
        symbol = random.choice(symbols)
        signal_class = random.randint(0, 3)
        r = random.random()
        if r < 0.60:
            severity = 1
        elif r < 0.90:
            severity = 2
        else:
            severity = 3
        conviction = round(0.40 + 0.60 * random.random(), 3)
        events.append(MarketEvent(
            symbol=symbol,
            signal_class=signal_class,
            severity=severity,
            conviction=conviction,
            timestamp_ms=1_000_000 + i * 1_000
        ))
    return events


def run_tests(events: List[MarketEvent]) -> Dict:
    translator = HapticTranslator()
    results = []
    failures = []

    for ev in events:
        hp = translator.translate(ev)
        intensities = [translator.PATTERNS[(ev.signal_class, s)][1] for s in (1, 2, 3)]
        if not (intensities[0] < intensities[1] < intensities[2]):
            failures.append(f"Intensity monotonicity broken for class {ev.signal_class}: {intensities}")
        durs = [translator.PATTERNS[(ev.signal_class, s)][2] for s in (1, 2, 3)]
        if not (durs[0] < durs[1] < durs[2]):
            failures.append(f"Duration monotonicity broken for class {ev.signal_class}: {durs}")
        if hp.location not in translator.LOCATION_NAMES:
            failures.append(f"Unknown location {hp.location}")

        frame = translator.to_frame(ev)
        if len(frame) != 8:
            failures.append(f"Frame length {len(frame)} != 8")
        if frame[0] != 0xAA:
            failures.append(f"Bad sync byte {frame[0]}")
        recomputed = sum(frame[:-1]) & 0xFF
        if recomputed != frame[-1]:
            failures.append(f"Checksum mismatch: {recomputed} != {frame[-1]}")

        results.append({
            'symbol': ev.symbol,
            'class': translator.CLASS_NAMES[ev.signal_class],
            'severity': translator.SEVERITY_NAMES[ev.severity],
            'pattern': hp.rhythm,
            'intensity': hp.intensity,
            'duration_ms': hp.duration_ms,
            'location': translator.LOCATION_NAMES[hp.location],
            'pattern_id': hp.pattern_id,
            'frame_hex': frame.hex()
        })

    sev_dist = {}
    loc_dist = {}
    cls_dist = {}
    for r in results:
        sev_dist[r['severity']] = sev_dist.get(r['severity'], 0) + 1
        loc_dist[r['location']] = loc_dist.get(r['location'], 0) + 1
        cls_dist[r['class']] = cls_dist.get(r['class'], 0) + 1

    return {
        'total': len(events),
        'translated': len(results),
        'unique_frames': len(set(r['frame_hex'] for r in results)),
        'failure_count': len(failures),
        'failures': failures[:5],
        'severity_distribution': sev_dist,
        'location_distribution': loc_dist,
        'class_distribution': cls_dist
    }


if __name__ == '__main__':
    events = generate_synthetic_events(1000)
    stats = run_tests(events)
    print(json.dumps(stats, indent=2))
