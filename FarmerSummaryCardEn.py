import os
import re
import webbrowser
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional, Dict, List, Tuple, Any
import xml.etree.ElementTree as ET


# -----------------------------
# File + XML helpers
# -----------------------------
def read_text_utf8(path: str) -> str:
    with open(path, "rb") as f:
        raw = f.read()
    return raw.decode("utf-8-sig", errors="replace")


def parse_xml_file(path: str) -> ET.Element:
    text = read_text_utf8(path).strip()
    return ET.fromstring(text)


def safe_int(v: Any) -> Optional[int]:
    if v is None:
        return None
    if isinstance(v, int):
        return v
    try:
        s = str(v).strip()
        if s == "":
            return None
        return int(s)
    except Exception:
        return None


def nz(v: Optional[int]) -> int:
    """None -> 0"""
    return 0 if v is None else v


def fmt_num(v: Optional[int]) -> str:
    # User requested: Unknown -> 0
    return f"{nz(v):,}"


def fmt_gold(v: Optional[int]) -> str:
    # User requested: Unknown -> 0
    return f"{nz(v):,}g"


def escape_html(s: Any) -> str:
    if s is None:
        return ""
    t = str(s)
    return (
        t.replace("&", "&amp;")
         .replace("<", "&lt;")
         .replace(">", "&gt;")
         .replace('"', "&quot;")
         .replace("'", "&#39;")
    )


def normalize_season(season: Optional[str]) -> Optional[str]:
    if not season:
        return None
    s = season.strip().lower()
    mapping = {"spring": "Spring", "summer": "Summer", "fall": "Fall", "autumn": "Fall", "winter": "Winter"}
    return mapping.get(s, season.strip().title())


def locate_main_save_file(path: str) -> str:
    """
    Accepts a save folder OR a file.
    If folder: prefers the main file with the same name as folder; else picks largest non-aux file.
    """
    path = os.path.abspath(path)

    if os.path.isfile(path):
        return path

    if not os.path.isdir(path):
        raise FileNotFoundError("Selected path is not a file or folder.")

    folder_name = os.path.basename(path.rstrip("\\/"))
    main_candidate = os.path.join(path, folder_name)
    if os.path.isfile(main_candidate):
        return main_candidate

    files = []
    for name in os.listdir(path):
        fp = os.path.join(path, name)
        if not os.path.isfile(fp):
            continue
        low = name.lower()
        if low == "savegameinfo":
            continue
        if low.endswith((".bak", ".old", ".tmp")):
            continue
        files.append(fp)

    if not files:
        raise FileNotFoundError("No save files found in that folder.")

    files.sort(key=lambda f: os.path.getsize(f), reverse=True)
    return files[0]


def default_saves_path() -> str:
    appdata = os.environ.get("APPDATA", "")
    if appdata:
        p = os.path.join(appdata, "StardewValley", "Saves")
        if os.path.isdir(p):
            return p
    return ""


def find_text(node: Optional[ET.Element], paths: List[str]) -> Optional[str]:
    if node is None:
        return None
    for p in paths:
        el = node.find(p)
        if el is not None and el.text and el.text.strip():
            return el.text.strip()
    return None


def farm_type_name(n: Optional[int]) -> str:
    if n is None:
        return "Unknown"
    mapping = {
        0: "Standard",
        1: "Riverland",
        2: "Forest",
        3: "Hill-top",
        4: "Wilderness",
        5: "Four Corners",
        6: "Beach",
        7: "Meadowlands (or modded)",
    }
    return f"{mapping.get(n, 'Type ' + str(n))} ({n})"


# -----------------------------
# Parsing Stardew structures
# -----------------------------
def find_root_and_player(root: ET.Element) -> Tuple[ET.Element, ET.Element]:
    """
    Returns (root_like, player_like).
    Supports: <SaveGame>, <SaveGameInfo>, <Farmer>, or odd nesting.
    """
    tag = root.tag
    if tag in ("SaveGame", "SaveGameInfo"):
        player = root.find("player") or root.find(".//player") or root.find(".//Farmer")
        if player is None:
            raise ValueError("Couldn't find <player> or <Farmer> in this save.")

        # Sometimes <player> wraps a single <Farmer>
        if player.tag == "player" and len(list(player)) == 1 and list(player)[0].tag == "Farmer":
            player = list(player)[0]

        return root, player

    if tag == "Farmer":
        return root, root

    farmer = root.find(".//Farmer")
    if farmer is not None:
        return root, farmer

    raise ValueError(f"Unrecognized XML root: <{tag}>")


def parse_stats_values(player: ET.Element) -> Dict[str, int]:
    """
    Parses player/stats/Values items:
      <item><key><string>stepsTaken</string></key>
           <value><unsignedInt>625870</unsignedInt></value></item>
    """
    out: Dict[str, int] = {}
    values = player.find("stats/Values") or player.find(".//stats/Values")
    if values is None:
        return out

    for item in values.findall("item"):
        k = item.findtext("key/string") or item.findtext("key") or ""
        k = k.strip()
        if not k:
            continue

        v_container = item.find("value")
        if v_container is None:
            continue

        v_text = None
        for child in list(v_container):
            if child.text and child.text.strip():
                v_text = child.text.strip()
                break
        if v_text is None and v_container.text and v_container.text.strip():
            v_text = v_container.text.strip()

        v = safe_int(v_text)
        if v is None:
            continue

        out[k] = v

    return out


def sum_dictionary_values(dict_node: Optional[ET.Element]) -> Optional[int]:
    """
    Sums a dictionary whose values are numeric:
      <value><int>12</int></value>
    """
    if dict_node is None:
        return None

    total = 0
    found = False

    for item in dict_node.findall("item"):
        v = item.find("value")
        if v is None:
            continue

        v_text = None
        for child in list(v):
            if child.text and child.text.strip():
                v_text = child.text.strip()
                break
        if v_text is None and v.text and v.text.strip():
            v_text = v.text.strip()

        n = safe_int(v_text)
        if n is None:
            continue

        total += n
        found = True

    return total if found else None


def sum_dictionary_first_int_in_array(dict_node: Optional[ET.Element]) -> Tuple[Optional[int], Optional[int]]:
    """
    For fishCaught-like dict where value is ArrayOfInt:
      <value><ArrayOfInt><int>COUNT</int><int>...</int></ArrayOfInt></value>
    Returns (types_count, total_count_sum)
    """
    if dict_node is None:
        return None, None

    items = dict_node.findall("item")
    if not items:
        return None, None

    types = 0
    total = 0
    found = False

    for item in items:
        k = item.findtext("key/string") or item.findtext("key/int") or item.findtext("key")
        if not k:
            continue

        # fishCaught: value/ArrayOfInt/int[0] is "times caught"
        v0 = None
        v0_el = item.find("value/ArrayOfInt/int")
        if v0_el is not None and v0_el.text and v0_el.text.strip():
            v0 = safe_int(v0_el.text)
        else:
            v0_text = item.findtext("value/int") or item.findtext("value/unsignedInt") or item.findtext("value/long")
            v0 = safe_int(v0_text)

        types += 1
        if v0 is not None:
            total += v0
            found = True

    return types, (total if found else None)


def count_items(node: Optional[ET.Element]) -> Optional[int]:
    if node is None:
        return None
    return len(node.findall("item"))


# -----------------------------
# Skills
# -----------------------------
SKILL_THRESHOLDS = [0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000]  # lv 0..10


def xp_to_level(xp: int) -> int:
    lvl = 0
    for i in range(1, len(SKILL_THRESHOLDS)):
        if xp >= SKILL_THRESHOLDS[i]:
            lvl = i
    return min(lvl, 10)


def level_progress(xp: int, level: int) -> Tuple[int, int, float]:
    """
    Returns (cur_start, next_target, pct0to1). For level 10: pct=1.
    """
    level = max(0, min(10, level))
    if level >= 10:
        return (SKILL_THRESHOLDS[10], SKILL_THRESHOLDS[10], 1.0)

    cur = SKILL_THRESHOLDS[level]
    nxt = SKILL_THRESHOLDS[level + 1]
    span = max(1, (nxt - cur))
    pct = (xp - cur) / span
    pct = max(0.0, min(1.0, pct))
    return (cur, nxt, pct)


def parse_experience_points(player: ET.Element) -> List[int]:
    xp_node = player.find("experiencePoints") or player.find("ExperiencePoints")
    if xp_node is None:
        return []
    arr = []
    for it in xp_node.findall("int"):
        arr.append(safe_int(it.text) or 0)
    return arr


# -----------------------------
# Data model
# -----------------------------
@dataclass
class CardData:
    farm_name: str
    farmer_name: str
    year: int
    season: Optional[str]
    day: int
    farm_type: Optional[int]
    game_version: Optional[str]

    total_money_earned: int
    current_money: int

    days_played: int
    items_shipped: int
    crops_shipped: int
    fish_caught: int
    monsters_killed: int
    steps_taken: int
    quests_completed: int
    artisan_goods: int

    cooking_recipes_known: int
    crafting_recipes_known: int
    fish_types: int
    minerals_types: int

    events_seen_count: int
    max_health: int
    deepest_mine: int
    mail_received_count: int

    spouse: Optional[str]

    skills: List[Tuple[str, int, int, float]]  # name, level, xp, pct to next
    stats_values: Dict[str, int]

    source_file: str


# -----------------------------
# Build card data
# -----------------------------
def build_card_data(save_path: str) -> CardData:
    root = parse_xml_file(save_path)
    root_like, player = find_root_and_player(root)

    farm_name = find_text(player, ["farmName", "farmName/value"]) or "Unknown"
    farmer_name = find_text(player, ["name", "name/value"]) or "Unknown"

    # Unknown -> 0 for numeric values
    year = nz(safe_int(find_text(root_like, ["year"])))
    day = nz(safe_int(find_text(root_like, ["dayOfMonth"])))
    season = normalize_season(find_text(root_like, ["currentSeason", "season"]))

    farm_type = safe_int(find_text(root_like, ["whichFarm", "farmType"]))
    game_version = find_text(root_like, ["gameVersion", "gameVersion/value"]) or find_text(player, ["gameVersion"])

    total_money_earned = nz(safe_int(find_text(player, ["totalMoneyEarned", "totalMoneyEarned/value"])))
    current_money = nz(safe_int(find_text(player, ["money", "money/value"])))

    spouse = find_text(player, ["spouse", "spouse/value"])

    stats = parse_stats_values(player)

    days_played = stats.get("daysPlayed", 0)

    # Items shipped: sum of shipped dictionary quantities
    items_shipped = nz(sum_dictionary_values(player.find("basicShipped")))

    crops_shipped = stats.get("cropsShipped", 0)
    monsters_killed = stats.get("monstersKilled", 0)
    steps_taken = stats.get("stepsTaken", 0)
    quests_completed = stats.get("questsCompleted", 0)

    # Fish caught: fishCaught dict uses ArrayOfInt; sum first int
    fish_types, fish_total = sum_dictionary_first_int_in_array(player.find("fishCaught"))
    fish_types = nz(fish_types)
    fish_caught = nz(fish_total)

    minerals_types = nz(count_items(player.find("mineralsFound")))
    cooking_recipes_known = nz(count_items(player.find("cookingRecipes")))
    crafting_recipes_known = nz(count_items(player.find("craftingRecipes")))

    events_seen = player.find("eventsSeen")
    events_seen_count = len(events_seen.findall("int")) if events_seen is not None else 0

    max_health = nz(safe_int(find_text(player, ["maxHealth", "MaxHealth"])))
    deepest_mine = nz(safe_int(find_text(player, ["deepestMineLevel", "deepestMineLevel/value"])))

    mail_received = player.find("mailReceived")
    mail_received_count = len(mail_received.findall("string")) if mail_received is not None else 0

    preserves = stats.get("preservesMade", 0)
    beverages = stats.get("beveragesMade", 0)
    cheese = stats.get("cheeseMade", 0)
    goat_cheese = stats.get("goatCheeseMade", 0)
    artisan_goods = preserves + beverages + cheese + goat_cheese  # missing -> 0 automatically

    xp = parse_experience_points(player)
    names = ["Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck"]
    level_tags = ["farmingLevel", "fishingLevel", "foragingLevel", "miningLevel", "combatLevel", "luckLevel"]

    skills = []
    for i, nm in enumerate(names):
        xp_i = xp[i] if i < len(xp) else 0
        lvl = safe_int(find_text(player, [level_tags[i]]))
        if lvl is None:
            lvl = xp_to_level(xp_i)
        _, _, pct = level_progress(xp_i, lvl)
        skills.append((nm, lvl, xp_i, pct))

    return CardData(
        farm_name=farm_name,
        farmer_name=farmer_name,
        year=year,
        season=season,
        day=day,
        farm_type=farm_type,
        game_version=game_version,

        total_money_earned=total_money_earned,
        current_money=current_money,

        days_played=days_played,
        items_shipped=items_shipped,
        crops_shipped=crops_shipped,
        fish_caught=fish_caught,
        monsters_killed=monsters_killed,
        steps_taken=steps_taken,
        quests_completed=quests_completed,
        artisan_goods=artisan_goods,

        cooking_recipes_known=cooking_recipes_known,
        crafting_recipes_known=crafting_recipes_known,
        fish_types=fish_types,
        minerals_types=minerals_types,

        events_seen_count=events_seen_count,
        max_health=max_health,
        deepest_mine=deepest_mine,
        mail_received_count=mail_received_count,

        spouse=spouse,

        skills=skills,
        stats_values=stats,
        source_file=os.path.basename(save_path)
    )


# -----------------------------
# Theme helpers
# -----------------------------
def season_theme(season: Optional[str]) -> Tuple[str, str]:
    s = (season or "").lower()
    if s == "spring":
        return "#34d399", "#10b981"   # emerald
    if s == "summer":
        return "#fbbf24", "#f59e0b"   # amber
    if s == "fall":
        return "#fb923c", "#f97316"   # orange
    if s == "winter":
        return "#60a5fa", "#3b82f6"   # sky/blue
    return "#a78bfa", "#8b5cf6"       # violet


# -----------------------------
# HTML Rendering (two-theme toggle, Social = spouse only)
# -----------------------------
def render_html(d: CardData) -> str:
    now = datetime.now().strftime("%Y-%m-%d %H:%M")
    accent, accent2 = season_theme(d.season)

    def tile(icon: str, title: str, value: str, subtitle: Optional[str] = None) -> str:
        sub = f'<div class="sub">{escape_html(subtitle)}</div>' if subtitle else ""
        return f"""
          <div class="tile">
            <div class="icon">{escape_html(icon)}</div>
            <div class="meta">
              <div class="t">{escape_html(title)}</div>
              <div class="v">{escape_html(value)}</div>
              {sub}
            </div>
          </div>
        """

    chips = []
    chips.append(f"Farmer: <b>{escape_html(d.farmer_name)}</b>")
    chips.append(f"Year <b>{d.year}</b>")
    if d.season:
        chips.append(f"{escape_html(d.season)} Day <b>{d.day}</b>")
    else:
        chips.append(f"Day <b>{d.day}</b>")
    if d.farm_type is not None:
        chips.append(f"Farm: <b>{escape_html(farm_type_name(d.farm_type))}</b>")
    if d.game_version:
        chips.append(f"Version: <b>{escape_html(d.game_version)}</b>")
    chips_html = "".join([f'<span class="chip">{c}</span>' for c in chips])

    highlights_html = "\n".join([
        tile("🗓️", "Days Played", fmt_num(d.days_played)),
        tile("📦", "Items Shipped", fmt_num(d.items_shipped), "Sum of basicShipped"),
        tile("🌾", "Crops Shipped", fmt_num(d.crops_shipped), "From stats.Values"),
        tile("🎣", "Fish Caught", fmt_num(d.fish_caught), "Sum of fishCaught counts"),
        tile("🗡️", "Monsters Killed", fmt_num(d.monsters_killed)),
        tile("👣", "Steps Taken", fmt_num(d.steps_taken)),
        tile("📜", "Quests Completed", fmt_num(d.quests_completed)),
        tile("🧺", "Artisan Goods", fmt_num(d.artisan_goods), "Preserves + beverages + cheese"),
    ])

    collections_html = "\n".join([
        tile("🍳", "Cooking recipes known", fmt_num(d.cooking_recipes_known)),
        tile("🛠️", "Crafting recipes known", fmt_num(d.crafting_recipes_known)),
        tile("🐟", "Fish caught (types)", fmt_num(d.fish_types)),
        tile("💎", "Minerals found (types)", fmt_num(d.minerals_types)),
    ])

    progress_html = "\n".join([
        tile("✨", "Events seen", fmt_num(d.events_seen_count)),
        tile("❤️", "Max health", fmt_num(d.max_health)),
        tile("⛏️", "Deepest mine", fmt_num(d.deepest_mine)),
        tile("📬", "Mail received", fmt_num(d.mail_received_count)),
    ])

    spouse_line = escape_html(d.spouse) if d.spouse else "None"

    # Skills (bars)
    skill_rows = []
    for nm, lvl, xp, pct in d.skills:
        pct100 = int(round(pct * 100))
        label = "Mastered" if lvl >= 10 else f"{pct100}% to next"
        skill_rows.append(
            f"""
            <div class="skill">
              <div class="sl">
                <div class="sn">{escape_html(nm)}</div>
                <div class="sx">XP: {fmt_num(xp)}</div>
              </div>
              <div class="sr">
                <div class="lv">Lv {lvl}</div>
                <div class="bar"><div class="fill" style="width:{pct100}%"></div></div>
                <div class="sb">{escape_html(label)}</div>
              </div>
            </div>
            """
        )
    skill_rows_html = "\n".join(skill_rows)

    stat_items = sorted(d.stats_values.items(), key=lambda kv: kv[0].lower())
    stats_rows = "\n".join(
        f"<tr><td class='k'>{escape_html(k)}</td><td class='vv'>{fmt_num(v)}</td></tr>"
        for k, v in stat_items
    )

    gold_big = fmt_gold(d.total_money_earned)
    money_small = fmt_gold(d.current_money)

    return f"""<!doctype html>
<html lang="en" data-theme="dusk">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Farm Summary Card</title>

<style>
  :root {{
    --accent: {accent};
    --accent2: {accent2};
    --r: 22px;

    /* defaults (overridden by [data-theme]) */
    --bg1: #0b1220;
    --bg2: #0f172a;
    --card: rgba(17, 25, 40, 0.72);
    --ink: #eaf0ff;
    --muted: rgba(234, 240, 255, 0.64);
    --line: rgba(234, 240, 255, 0.12);
    --tile: rgba(234, 240, 255, 0.06);
    --shadow: 0 24px 80px rgba(0,0,0,0.55);
    --chip: rgba(255,255,255,0.06);
    --chipBorder: rgba(234,240,255,0.12);
    --inputBg: rgba(255,255,255,0.06);
    --panelBg: rgba(255,255,255,0.04);
    --tableBg: rgba(255,255,255,0.03);
    --iconBg: rgba(255,255,255,0.06);
  }}

  html[data-theme="dusk"] {{
    --bg1: #0b1220;
    --bg2: #0f172a;
    --card: rgba(17, 25, 40, 0.72);
    --ink: #eaf0ff;
    --muted: rgba(234, 240, 255, 0.64);
    --line: rgba(234, 240, 255, 0.12);
    --tile: rgba(234, 240, 255, 0.06);
    --shadow: 0 24px 80px rgba(0,0,0,0.55);
    --chip: rgba(255,255,255,0.06);
    --chipBorder: rgba(234,240,255,0.12);
    --inputBg: rgba(255,255,255,0.06);
    --panelBg: rgba(255,255,255,0.04);
    --tableBg: rgba(255,255,255,0.03);
    --iconBg: rgba(255,255,255,0.06);
  }}

  html[data-theme="paper"] {{
    --bg1: #f3efe6;
    --bg2: #efe7d8;
    --card: rgba(255, 255, 255, 0.82);
    --ink: #1f2937;
    --muted: rgba(31, 41, 55, 0.62);
    --line: rgba(31, 41, 55, 0.14);
    --tile: rgba(31, 41, 55, 0.05);
    --shadow: 0 24px 80px rgba(31,41,55,0.18);
    --chip: rgba(255,255,255,0.70);
    --chipBorder: rgba(31,41,55,0.12);
    --inputBg: rgba(255,255,255,0.85);
    --panelBg: rgba(255,255,255,0.62);
    --tableBg: rgba(255,255,255,0.70);
    --iconBg: rgba(255,255,255,0.85);
  }}

  * {{ box-sizing: border-box; }}
  body {{
    margin: 0;
    min-height: 100vh;
    display: grid;
    place-items: center;
    padding: 28px 18px;
    color: var(--ink);
    font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial;
    background:
      radial-gradient(1100px 640px at 18% 0%, var(--accent2), transparent 66%),
      radial-gradient(900px 580px at 82% 10%, rgba(255,255,255,0.08), transparent 62%),
      linear-gradient(180deg, var(--bg1), var(--bg2));
  }}

  .card {{
    width: 1120px;
    max-width: 100%;
    border-radius: 28px;
    background: var(--card);
    border: 1px solid var(--line);
    box-shadow: var(--shadow);
    overflow: hidden;
    backdrop-filter: blur(12px);
  }}

  .hero {{
    padding: 26px 28px 18px 28px;
    position: relative;
  }}

  .top {{
    display: flex;
    gap: 18px;
    justify-content: space-between;
    align-items: flex-start;
    flex-wrap: wrap;
    position: relative;
  }}

  .farm {{
    font-size: 40px;
    font-weight: 950;
    letter-spacing: -0.02em;
    line-height: 1.05;
  }}

  .chips {{
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
    color: var(--muted);
    font-size: 13px;
    margin-top: 10px;
  }}

  .chip {{
    padding: 8px 12px;
    border-radius: 999px;
    border: 1px solid var(--chipBorder);
    background: var(--chip);
  }}

  .money {{
    text-align: right;
    min-width: 270px;
    display: grid;
    gap: 6px;
  }}

  .money .big {{
    font-size: 36px;
    font-weight: 950;
    letter-spacing: -0.02em;
  }}

  .money .lbl, .money .small {{
    font-size: 12px;
    color: var(--muted);
  }}

  .controls {{
    margin-top: 12px;
    display: flex;
    gap: 10px;
    align-items: center;
    flex-wrap: wrap;
  }}

  .toggle {{
    display: inline-flex;
    gap: 10px;
    align-items: center;
    border: 1px solid var(--line);
    background: var(--chip);
    padding: 8px 10px;
    border-radius: 999px;
    cursor: pointer;
    user-select: none;
    font-size: 13px;
    color: var(--muted);
    font-weight: 800;
  }}

  .toggle b {{
    color: var(--ink);
    font-weight: 950;
  }}

  .divider {{
    height: 1px;
    background: var(--line);
    margin: 0 28px;
  }}

  .grid {{
    display: grid;
    grid-template-columns: 1.18fr 0.82fr;
    gap: 16px;
    padding: 18px 28px 26px 28px;
  }}

  @media (max-width: 980px) {{
    .grid {{ grid-template-columns: 1fr; }}
    .money {{ text-align: left; }}
  }}

  .panel {{
    border: 1px solid var(--line);
    border-radius: var(--r);
    padding: 16px;
    background: var(--panelBg);
  }}

  .h {{
    margin: 0 0 12px 0;
    font-size: 12px;
    letter-spacing: 0.10em;
    text-transform: uppercase;
    color: var(--muted);
    font-weight: 950;
  }}

  .tiles {{
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 10px;
  }}

  .tile {{
    display: flex;
    gap: 12px;
    padding: 12px;
    border-radius: 16px;
    background: var(--tile);
    border: 1px solid var(--line);
    align-items: center;
  }}

  .icon {{
    width: 38px;
    height: 38px;
    border-radius: 14px;
    border: 1px solid var(--line);
    background: var(--iconBg);
    display: grid;
    place-items: center;
    font-size: 18px;
  }}

  .t {{
    font-size: 12px;
    color: var(--muted);
    font-weight: 900;
  }}

  .v {{
    font-size: 20px;
    font-weight: 950;
    margin-top: 4px;
  }}

  .sub {{
    font-size: 11px;
    color: color-mix(in srgb, var(--muted) 85%, transparent);
    margin-top: 3px;
    font-weight: 700;
  }}

  .skills {{ display: grid; gap: 10px; }}

  .skill {{
    border: 1px solid var(--line);
    background: var(--tile);
    border-radius: 16px;
    padding: 12px;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 10px;
    align-items: center;
  }}

  @media (max-width: 680px) {{
    .skill {{ grid-template-columns: 1fr; }}
  }}

  .sn {{ font-weight: 950; }}
  .sx {{ font-size: 12px; color: var(--muted); margin-top: 2px; font-weight: 700; }}
  .sr {{ text-align: right; }}
  @media (max-width: 680px) {{ .sr {{ text-align: left; }} }}

  .lv {{ font-weight: 950; }}

  .bar {{
    margin-top: 8px;
    height: 10px;
    border-radius: 999px;
    border: 1px solid var(--line);
    background: var(--inputBg);
    overflow: hidden;
  }}

  .fill {{
    height: 100%;
    background: linear-gradient(90deg, var(--accent), rgba(255,255,255,0.0));
    width: 0%;
  }}

  .sb {{
    font-size: 11px;
    color: var(--muted);
    margin-top: 6px;
    font-weight: 800;
  }}

  /* Social: spouse only */
  .socialOne {{
    display: grid;
    grid-template-columns: 1fr;
  }}

  .statPill {{
    padding: 12px;
    border-radius: 16px;
    border: 1px solid var(--line);
    background: var(--tile);
  }}

  .statPill .k {{ font-size: 12px; color: var(--muted); font-weight: 900; }}
  .statPill .vv {{ font-size: 18px; font-weight: 950; margin-top: 6px; }}

  details {{
    margin-top: 14px;
    border: 1px solid var(--line);
    border-radius: 16px;
    background: var(--panelBg);
    padding: 12px;
  }}

  summary {{
    cursor: pointer;
    user-select: none;
    list-style: none;
    font-weight: 950;
    color: var(--muted);
  }}
  summary::-webkit-details-marker {{ display: none; }}

  .search {{
    margin-top: 10px;
    display: flex;
    gap: 10px;
    align-items: center;
  }}

  .search input {{
    width: 100%;
    padding: 10px 12px;
    border-radius: 14px;
    border: 1px solid var(--line);
    background: var(--inputBg);
    outline: none;
    font-size: 13px;
    color: var(--ink);
  }}

  .search input::placeholder {{
    color: color-mix(in srgb, var(--muted) 75%, transparent);
  }}

  .search .count {{
    font-size: 12px;
    color: var(--muted);
    font-weight: 800;
    white-space: nowrap;
  }}

  .tableWrap {{
    margin-top: 10px;
    max-height: 420px;
    overflow: auto;
    border-radius: 14px;
    border: 1px solid rgba(127,127,127,0.18);
    background: var(--tableBg);
  }}

  table {{ width: 100%; border-collapse: collapse; font-size: 12px; }}
  td {{ padding: 10px 12px; border-bottom: 1px solid rgba(127,127,127,0.16); }}
  td.k {{ color: var(--muted); font-weight: 900; }}
  td.vv {{ text-align: right; font-weight: 950; }}

  .foot {{
    display: flex;
    justify-content: space-between;
    gap: 10px;
    padding: 0 28px 22px 28px;
    color: var(--muted);
    font-size: 12px;
    font-weight: 700;
  }}
</style>
</head>

<body>
  <div class="card">
    <div class="hero">
      <div class="top">
        <div class="title">
          <div class="farm">{escape_html(d.farm_name)} Farm</div>
          <div class="chips">{chips_html}</div>

          <div class="controls">
            <button class="toggle" id="themeBtn" type="button" aria-label="Toggle theme">
              <span id="themeIcon">🌙</span>
              Theme: <b id="themeName">Soft Dusk</b>
            </button>
          </div>
        </div>

        <div class="money">
          <div class="big">{escape_html(gold_big)}</div>
          <div class="lbl">Total Gold Earned</div>
          <div class="small">Current Money: <b>{escape_html(money_small)}</b></div>
        </div>
      </div>
    </div>

    <div class="divider"></div>

    <div class="grid">
      <div class="panel">
        <div class="h">Highlights</div>
        <div class="tiles">{highlights_html}</div>

        <div style="height:16px"></div>

        <div class="h">Skills</div>
        <div class="skills">{skill_rows_html}</div>
      </div>

      <div class="panel">
        <div class="h">Collections</div>
        <div class="tiles">{collections_html}</div>

        <div style="height:16px"></div>

        <div class="h">Progress</div>
        <div class="tiles">{progress_html}</div>

        <div style="height:16px"></div>

        <div class="h">Social</div>
        <div class="socialOne">
          <div class="statPill">
            <div class="k">Spouse</div>
            <div class="vv">{spouse_line}</div>
          </div>
        </div>

        <details>
          <summary>All Player Stats (stats.Values) · <span id="statCount">{len(stat_items)}</span> fields</summary>
          <div class="search">
            <input id="statSearch" type="text" placeholder="Filter stats by key…" />
            <div class="count"><span id="shownCount">{len(stat_items)}</span> shown</div>
          </div>
          <div class="tableWrap">
            <table id="statsTable"><tbody>{stats_rows}</tbody></table>
          </div>
        </details>
      </div>
    </div>

    <div class="foot">
      <div>Generated: {escape_html(now)}</div>
      <div>Source: {escape_html(d.source_file)}</div>
    </div>
  </div>

<script>
(function() {{
  // Theme toggle with persistence
  const html = document.documentElement;
  const btn = document.getElementById("themeBtn");
  const name = document.getElementById("themeName");
  const icon = document.getElementById("themeIcon");

  function applyTheme(theme) {{
    html.setAttribute("data-theme", theme);
    if (theme === "paper") {{
      name.textContent = "Warm Paper";
      icon.textContent = "☀️";
    }} else {{
      name.textContent = "Soft Dusk";
      icon.textContent = "🌙";
    }}
    try {{ localStorage.setItem("farmCardTheme", theme); }} catch (e) {{}}
  }}

  let saved = null;
  try {{ saved = localStorage.getItem("farmCardTheme"); }} catch (e) {{}}
  applyTheme(saved === "paper" ? "paper" : "dusk");

  if (btn) {{
    btn.addEventListener("click", () => {{
      const cur = html.getAttribute("data-theme") || "dusk";
      applyTheme(cur === "dusk" ? "paper" : "dusk");
    }});
  }}

  // Stats filter
  const input = document.getElementById("statSearch");
  const table = document.getElementById("statsTable");
  const shown = document.getElementById("shownCount");
  if (input && table && shown) {{
    const rows = Array.from(table.querySelectorAll("tr"));
    function update() {{
      const q = (input.value || "").toLowerCase().trim();
      let c = 0;
      for (const r of rows) {{
        const k = (r.querySelector(".k")?.textContent || "").toLowerCase();
        const ok = !q || k.includes(q);
        r.style.display = ok ? "" : "none";
        if (ok) c++;
      }}
      shown.textContent = String(c);
    }}
    input.addEventListener("input", update);
    update();
  }}
}})();
</script>
</body>
</html>
"""


# -----------------------------
# Tkinter UI
# -----------------------------
class FarmSummaryApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Stardew Farm Summary Card (HTML)")
        self.geometry("920x620")
        self.minsize(880, 600)
        self.configure(bg="#f6f8fc")

        self.path_var = tk.StringVar(value="")
        self.out_var = tk.StringVar(value=str(Path.home() / "Desktop"))

        self._style()
        self._ui()

    def _style(self):
        s = ttk.Style(self)
        try:
            s.theme_use("clam")
        except Exception:
            pass

        s.configure(".", font=("Segoe UI", 10))
        s.configure("TFrame", background="#f6f8fc")
        s.configure("Card.TFrame", background="#ffffff")
        s.configure("Header.TLabel", background="#ffffff", foreground="#0f172a",
                    font=("Segoe UI", 16, "bold"))
        s.configure("Sub.TLabel", background="#ffffff", foreground="#475569",
                    font=("Segoe UI", 10))
        s.configure("TEntry", padding=(10, 8))
        s.configure("TButton", padding=(12, 9))
        s.configure("Accent.TButton", padding=(14, 10))

    def _ui(self):
        outer = ttk.Frame(self, padding=18)
        outer.pack(fill="both", expand=True)

        card = ttk.Frame(outer, style="Card.TFrame", padding=18)
        card.pack(fill="both", expand=True)

        ttk.Label(card, text="Farm Summary Card", style="Header.TLabel").pack(anchor="w")
        ttk.Label(
            card,
            text="Select a save folder or file. Generates one HTML page (Unknown -> 0, theme toggle included).",
            style="Sub.TLabel"
        ).pack(anchor="w", pady=(4, 0))

        box = ttk.Frame(card, style="Card.TFrame")
        box.pack(fill="x", pady=(16, 0))

        ttk.Label(box, text="Save folder or save file", style="Sub.TLabel").pack(anchor="w")
        row = ttk.Frame(box, style="Card.TFrame")
        row.pack(fill="x", pady=(6, 0))

        ttk.Entry(row, textvariable=self.path_var).pack(side="left", fill="x", expand=True)
        ttk.Button(row, text="Browse Folder", command=self.pick_folder).pack(side="left", padx=(10, 0))
        ttk.Button(row, text="Browse File", command=self.pick_file).pack(side="left", padx=(10, 0))

        out = ttk.Frame(card, style="Card.TFrame")
        out.pack(fill="x", pady=(14, 0))

        ttk.Label(out, text="Output folder", style="Sub.TLabel").pack(anchor="w")
        row2 = ttk.Frame(out, style="Card.TFrame")
        row2.pack(fill="x", pady=(6, 0))
        ttk.Entry(row2, textvariable=self.out_var).pack(side="left", fill="x", expand=True)
        ttk.Button(row2, text="Choose", command=self.pick_out).pack(side="left", padx=(10, 0))

        act = ttk.Frame(card, style="Card.TFrame")
        act.pack(fill="x", pady=(16, 0))
        ttk.Button(act, text="Generate HTML", style="Accent.TButton", command=self.generate).pack(side="left")
        ttk.Button(act, text="Open Saves Folder", command=self.open_saves).pack(side="left", padx=(10, 0))
        ttk.Button(act, text="Open Output Folder", command=self.open_output).pack(side="left", padx=(10, 0))

        self.log = tk.Text(card, height=12, wrap="word", bd=0, bg="#f3f4f6", fg="#0f172a",
                           font=("Consolas", 10))
        self.log.pack(fill="both", expand=True, pady=(16, 0))
        self._log("Tip: selecting the SAVE FOLDER is easiest.\n")

    def _log(self, msg: str):
        self.log.insert("end", msg)
        self.log.see("end")

    def pick_folder(self):
        initial = default_saves_path() or os.getcwd()
        p = filedialog.askdirectory(title="Select Stardew save folder", initialdir=initial)
        if p:
            self.path_var.set(p)
            self._log(f"Folder: {p}\n")

    def pick_file(self):
        initial = default_saves_path() or os.getcwd()
        p = filedialog.askopenfilename(
            title="Select Stardew save file",
            initialdir=initial,
            filetypes=[("All files", "*.*")]
        )
        if p:
            self.path_var.set(p)
            self._log(f"File: {p}\n")

    def pick_out(self):
        p = filedialog.askdirectory(title="Select output folder", initialdir=self.out_var.get() or os.getcwd())
        if p:
            self.out_var.set(p)
            self._log(f"Output: {p}\n")

    def open_saves(self):
        p = default_saves_path()
        if not p:
            messagebox.showinfo("Not found", "Could not detect the Stardew Saves folder automatically.")
            return
        try:
            os.startfile(p)
        except Exception:
            webbrowser.open("file://" + p)

    def open_output(self):
        p = self.out_var.get().strip() or os.getcwd()
        if not os.path.isdir(p):
            messagebox.showerror("Missing folder", "Output folder does not exist.")
            return
        try:
            os.startfile(p)
        except Exception:
            webbrowser.open("file://" + p)

    def generate(self):
        try:
            p = self.path_var.get().strip()
            if not p:
                messagebox.showwarning("Missing", "Please select a save folder or save file.")
                return

            save_file = locate_main_save_file(p)
            out_dir = (self.out_var.get() or "").strip() or os.getcwd()
            os.makedirs(out_dir, exist_ok=True)

            self._log("\nParsing save…\n")
            data = build_card_data(save_file)

            html = render_html(data)
            stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            safe_name = re.sub(r"[^A-Za-z0-9_-]+", "_", data.farm_name or "Farm")[:60]
            out_path = os.path.join(out_dir, f"FarmSummary_{safe_name}_{stamp}.html")

            with open(out_path, "w", encoding="utf-8") as f:
                f.write(html)

            self._log(f"Saved: {out_path}\n")
            webbrowser.open("file:///" + out_path.replace(os.sep, "/"))

        except Exception as e:
            messagebox.showerror("Failed", f"{e}")
            self._log(f"\nERROR: {e}\n")


if __name__ == "__main__":
    FarmSummaryApp().mainloop()
