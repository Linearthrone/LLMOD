import sqlite3
import json
import datetime

DB = r"c:\Users\kurtw\LLMOD\LLMOD-max-master\Data\Memory\HouseVictoria.db"


def entries(de):
    try:
        return json.loads(de) if de else []
    except Exception:
        return []


def main():
    c = sqlite3.connect(DB)
    cur = c.cursor()

    def merge_into(keep_id, other_ids):
        base = entries(cur.execute(
            "SELECT DataEntries FROM DataBanks WHERE Id=?", (keep_id,)).fetchone()[0])
        seen = {e.get("Id") for e in base if e.get("Id")}
        for oid in other_ids:
            row = cur.execute(
                "SELECT DataEntries FROM DataBanks WHERE Id=?", (oid,)).fetchone()
            if not row:
                continue
            for e in entries(row[0]):
                eid = e.get("Id")
                if eid and eid in seen:
                    continue
                if eid:
                    seen.add(eid)
                base.append(e)
        cur.execute(
            "UPDATE DataBanks SET DataEntries=?, LastModified=? WHERE Id=?",
            (json.dumps(base), datetime.datetime.now().isoformat("T"), keep_id))
        return len(base)

    # Forex merge: keep FOREX, merge Forex Databank + empty Forex rows away
    forex_keep = cur.execute(
        "SELECT Id FROM DataBanks WHERE Name='FOREX' AND length(DataEntries)>2").fetchone()[0]
    forex_other = [r[0] for r in cur.execute(
        "SELECT Id FROM DataBanks WHERE Name IN ('Forex Databank','Forex')").fetchall()]
    n = merge_into(forex_keep, forex_other)
    print("FOREX merged ->", n, "entries")

    # Dropped Files merge: keep the largest, merge the rest
    df_rows = cur.execute(
        "SELECT Id FROM DataBanks WHERE Name='Dropped Files' ORDER BY length(DataEntries) DESC").fetchall()
    df_keep = df_rows[0][0]
    df_other = [r[0] for r in df_rows[1:]]
    n = merge_into(df_keep, df_other)
    print("Dropped Files merged ->", n, "entries")

    # Victoria: keep the one that has entries
    vic_keep = cur.execute(
        "SELECT Id FROM DataBanks WHERE Name='Victoria - Personal Data' AND length(DataEntries)>2").fetchone()[0]

    # Keep every bank that still has entries after merges
    keep_ids = {forex_keep, df_keep, vic_keep}
    for rid, de in cur.execute("SELECT Id, DataEntries FROM DataBanks").fetchall():
        if len(entries(de)) > 0:
            keep_ids.add(rid)

    # Ensure merged-away duplicates are removed even though they briefly had data
    keep_ids -= (set(forex_other) - {forex_keep})
    keep_ids -= (set(df_other) - {df_keep})

    all_ids = [r[0] for r in cur.execute("SELECT Id FROM DataBanks").fetchall()]
    to_delete = [i for i in all_ids if i not in keep_ids]
    cur.executemany("DELETE FROM DataBanks WHERE Id=?", [(i,) for i in to_delete])
    c.commit()

    print("deleted", len(to_delete), "banks; kept", len(keep_ids))
    print("-" * 60)
    for name, de in cur.execute("SELECT Name, DataEntries FROM DataBanks ORDER BY Name").fetchall():
        print(f"{name[:40]:40} entries={len(entries(de))}")


if __name__ == "__main__":
    main()
