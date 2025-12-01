import re
import os
import sys
import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from dataclasses import dataclass, field
from typing import List, Optional


# =========================
# Translation dictionary
# =========================

TEXT = {
    "en": {
        # window
        "app_title": "SMAPI Log Doctor",
        "btn_open": "Open SMAPI Log",
        "btn_export": "Export Summary",
        "status_ready": "Ready. Open a SMAPI log to analyze.",
        "status_loaded": "Loaded log: {path}",
        "status_no_analysis": "No analysis yet. Open a log first.",
        "status_export_ok": "Summary exported to {path}",
        "status_export_fail": "Failed to export summary: {error}",

        # tabs
        "tab_overview": "Overview",
        "tab_mod_health": "Mod Health",
        "tab_errors": "Errors",
        "tab_warnings": "Warnings",
        "tab_suggestions": "Suggestions",
        "tab_raw": "Raw Log",

        # overview
        "overview_title": "Stardew Valley / SMAPI Overview",
        "overview_game_version": "Game version",
        "overview_smapi_version": "SMAPI version",
        "overview_unknown": "Unknown",
        "overview_summary": "Summary",
        "overview_mod_count": "Mods loaded: {count}",
        "overview_content_pack_count": "Content packs loaded: {count}",
        "overview_error_count": "Errors: {count}",
        "overview_warning_count": "Warnings: {count}",
        "overview_slow_start": "Startup time: {seconds:.1f}s",
        "overview_hint": "Tip: fix errors first, then warnings, then consistency / cosmetic issues.",

        # mod health
        "mod_health_title": "Mod Health & Risk",
        "mod_health_patched_header": "Mods patching game code (higher risk):",
        "mod_health_save_header": "Mods changing save serializer (do NOT remove mid-playthrough):",
        "mod_health_console_header": "Mods with direct console access:",
        "mod_health_missing_dep_header": "Mods with missing dependencies:",
        "mod_health_missing_dep_item": "{mod} → missing: {missing}",
        "mod_health_none": "No risky mods detected in this log.",
        "mod_health_updates_header": "Mods with updates available:",
        "mod_health_update_item": "{name} {current} → {latest}",

        # errors
        "errors_header": "Errors found in this log",
        "errors_none": "No SMAPI errors detected. 🎉",
        "errors_intro": "These are the most important issues reported by SMAPI:",

        # warnings
        "warnings_header": "Warnings",
        "warnings_none": "No warnings found.",
        "warnings_intro": "These may not break your game immediately, but are worth checking:",

        # suggestions
        "suggestions_header": "Suggested fixes",
        "suggestions_none": "No automatic suggestions. If the game still misbehaves, check Errors/Warn tabs.",

        # raw
        "raw_header": "Full SMAPI Log",

        # generic issues
        "warn_rivatuner": "RivaTuner Statistics Server detected. It can cause crashes with SMAPI; add an exception or disable it.",

        # suggestion types
        "sg.skipped_mod": "Fix mod \"{name}\": SMAPI skipped it ({reason}). Open its folder and ensure it has a valid manifest.json and is for your game/SMAPI version.",
        "sg.failed_mod": "Fix mod \"{name}\": SMAPI failed to load it ({reason}). Check the install instructions on its Nexus/Mod page.",
        "sg.missing_dep": "Install required dependency \"{missing}\" for \"{mod}\", or disable the dependent mod if you don't need it.",
        "sg.save_serializer": "\"{mod}\" changes the save serializer. Back up your saves and avoid removing this mod mid-playthrough.",
        "sg.patched_mods_many": "You have many mods patching game code ({count}). If you see weird crashes, try disabling utility/FX mods one by one.",
        "sg.rivatuner": "RivaTuner Statistics Server may conflict with SMAPI. Add an exception for Stardew Valley or close it while playing.",
        "sg.updates": "You can update {count} mods. Keeping frameworks and core mods updated often fixes crashes and invisible issues.",
        "sg.slow_start": "Game startup took about {seconds:.1f}s. Large content packs and many patching mods can increase load time; consider trimming heavy mods if this bothers you.",
    },
    "zh": {
        # window
        "app_title": "SMAPI 日志小医生",
        "btn_open": "打开 SMAPI 日志",
        "btn_export": "导出概览报告",
        "status_ready": "就绪。先打开一份 SMAPI 日志再分析。",
        "status_loaded": "已加载日志：{path}",
        "status_no_analysis": "还没有分析结果，请先打开一份日志。",
        "status_export_ok": "已导出总结到 {path}",
        "status_export_fail": "导出总结失败：{error}",

        # tabs
        "tab_overview": "概览",
        "tab_mod_health": "模组健康",
        "tab_errors": "错误",
        "tab_warnings": "警告",
        "tab_suggestions": "解决方案",
        "tab_raw": "原始日志",

        # overview
        "overview_title": "星露谷 / SMAPI 概览",
        "overview_game_version": "游戏版本",
        "overview_smapi_version": "SMAPI 版本",
        "overview_unknown": "未知",
        "overview_summary": "总结",
        "overview_mod_count": "已加载模组数量：{count}",
        "overview_content_pack_count": "已加载内容包数量：{count}",
        "overview_error_count": "错误数：{count}",
        "overview_warning_count": "警告数：{count}",
        "overview_slow_start": "启动耗时：{seconds:.1f} 秒",
        "overview_hint": "小提示：先解决“错误”，再看“警告”，最后再收拾体验/外观类问题。",

        # mod health
        "mod_health_title": "模组健康与风险",
        "mod_health_patched_header": "直接修改游戏代码的模组（风险较高）：",
        "mod_health_save_header": "改变存档序列化的模组（请勿中途移除）：",
        "mod_health_console_header": "直接读写控制台的模组：",
        "mod_health_missing_dep_header": "缺少前置依赖的模组：",
        "mod_health_missing_dep_item": "{mod} → 缺少：{missing}",
        "mod_health_none": "本次日志中没有检测到明显高风险模组。",
        "mod_health_updates_header": "有可用更新的模组：",
        "mod_health_update_item": "{name} {current} → {latest}",

        # errors
        "errors_header": "本日志中发现的错误",
        "errors_none": "未检测到 SMAPI 错误。🎉",
        "errors_intro": "下面是 SMAPI 报告的关键问题：",

        # warnings
        "warnings_header": "警告",
        "warnings_none": "未发现警告。",
        "warnings_intro": "这些问题不一定马上导致崩溃，但建议检查：",

        # suggestions
        "suggestions_header": "推荐解决方案",
        "suggestions_none": "暂时没有自动建议。如果游戏仍有问题，请优先查看“错误”和“警告”标签页。",

        # raw
        "raw_header": "完整 SMAPI 日志",

        # generic issues
        "warn_rivatuner": "检测到 RivaTuner Statistics Server，它可能与 SMAPI 冲突，建议为星露谷添加例外或在游玩时关闭。",

        # suggestion types
        "sg.skipped_mod": "修复模组“{name}”：该模组被 SMAPI 跳过（原因：{reason}）。请检查模组文件夹中是否有有效的 manifest.json，并确认模组版本支持当前游戏/SMAPI 版本。",
        "sg.failed_mod": "修复模组“{name}”：SMAPI 无法加载它（原因：{reason}）。请前往模组页面查看安装说明，必要时重新安装。",
        "sg.missing_dep": "为“{mod}”安装必需的前置模组“{missing}”，如果不需要该模组，也可以直接禁用它。",
        "sg.save_serializer": "“{mod}”更改了存档写入方式。请务必先备份存档，且不要在存档周目中途移除该模组。",
        "sg.patched_mods_many": "你当前有较多模组在修改游戏底层代码（共 {count} 个）。如果遇到奇怪的报错或崩溃，可以优先尝试禁用部分工具/特效类模组进行排查。",
        "sg.rivatuner": "RivaTuner Statistics Server 可能与 SMAPI 冲突。建议为星露谷添加例外或在游玩时暂时关闭该软件。",
        "sg.updates": "有 {count} 个模组可以更新。优先更新框架/核心模组，通常可以修复崩溃和一些看不见的兼容问题。",
        "sg.slow_start": "本次游戏启动大约耗时 {seconds:.1f} 秒。大量内容包和修改底层代码的模组会拉长加载时间，如有需要可以考虑精简大型模组。",
    },
    "ru": {
        # window
        "app_title": "Доктор логов SMAPI",
        "btn_open": "Открыть лог SMAPI",
        "btn_export": "Экспортировать сводку",
        "status_ready": "Готово. Сначала откройте лог SMAPI для анализа.",
        "status_loaded": "Лог загружен: {path}",
        "status_no_analysis": "Анализа ещё нет. Сначала откройте лог.",
        "status_export_ok": "Сводка сохранена в {path}",
        "status_export_fail": "Не удалось экспортировать сводку: {error}",

        # tabs
        "tab_overview": "Обзор",
        "tab_mod_health": "Состояние модов",
        "tab_errors": "Ошибки",
        "tab_warnings": "Предупреждения",
        "tab_suggestions": "Решения",
        "tab_raw": "Исходный лог",

        # overview
        "overview_title": "Обзор Stardew Valley / SMAPI",
        "overview_game_version": "Версия игры",
        "overview_smapi_version": "Версия SMAPI",
        "overview_unknown": "Неизвестно",
        "overview_summary": "Краткая сводка",
        "overview_mod_count": "Загружено модов: {count}",
        "overview_content_pack_count": "Загружено контент-паков: {count}",
        "overview_error_count": "Ошибок: {count}",
        "overview_warning_count": "Предупреждений: {count}",
        "overview_slow_start": "Время запуска: {seconds:.1f} с",
        "overview_hint": "Подсказка: сначала исправляйте ошибки, потом предупреждения, а уже затем косметику и оптимизацию.",

        # mod health
        "mod_health_title": "Состояние и риск модов",
        "mod_health_patched_header": "Моды, патчащие игровой код (повышенный риск):",
        "mod_health_save_header": "Моды, изменяющие сериализацию сохранений (нельзя удалять в середине прохождения):",
        "mod_health_console_header": "Моды с прямым доступом к консоли:",
        "mod_health_missing_dep_header": "Моды с отсутствующими зависимостями:",
        "mod_health_missing_dep_item": "{mod} → отсутствует: {missing}",
        "mod_health_none": "В этом логе не обнаружено явно рискованных модов.",
        "mod_health_updates_header": "Моды с доступными обновлениями:",
        "mod_health_update_item": "{name} {current} → {latest}",

        # errors
        "errors_header": "Ошибки в этом логе",
        "errors_none": "Ошибок SMAPI не найдено. 🎉",
        "errors_intro": "Это наиболее важные проблемы, о которых сообщает SMAPI:",

        # warnings
        "warnings_header": "Предупреждения",
        "warnings_none": "Предупреждений не найдено.",
        "warnings_intro": "Они не всегда ломают игру сразу, но на них стоит взглянуть:",

        # suggestions
        "suggestions_header": "Рекомендуемые действия",
        "suggestions_none": "Автоматических рекомендаций нет. Если игра по-прежнему ведёт себя странно, загляните на вкладки «Ошибки» и «Предупреждения».",

        # raw
        "raw_header": "Полный лог SMAPI",

        # generic issues
        "warn_rivatuner": "Обнаружен RivaTuner Statistics Server. Он может вызывать вылеты с SMAPI; добавьте исключение или отключите его.",

        # suggestion types
        "sg.skipped_mod": "Исправьте мод {name}: SMAPI пропустил его (причина: {reason}). Откройте его папку и проверьте manifest.json и совместимость с вашей версией игры/SMAPI.",
        "sg.failed_mod": "Исправьте мод {name}: SMAPI не смог его загрузить (причина: {reason}). Проверьте инструкцию по установке на странице мода и при необходимости переустановите.",
        "sg.missing_dep": "Установите обязательную зависимость {missing} для мода {mod}, либо отключите этот мод, если он вам не нужен.",
        "sg.save_serializer": "{mod} изменяет способ сохранения. Обязательно сделайте резервную копию сейвов и не удаляйте этот мод посреди прохождения.",
        "sg.patched_mods_many": "У вас много модов, патчащих игровой код ({count}). Если видите странные вылеты, попробуйте временно отключать утилиты/FX-моды по одному.",
        "sg.rivatuner": "RivaTuner Statistics Server может конфликтовать с SMAPI. Добавьте для Stardew Valley исключение или закройте программу во время игры.",
        "sg.updates": "Доступны обновления для {count} мод(ов). Обновление фреймворков и базовых модов часто устраняет вылеты и скрытые проблемы.",
        "sg.slow_start": "Запуск игры занял около {seconds:.1f} с. Большие контент-паки и множество «тяжёлых» модов увеличивают время загрузки; при желании можно немного почистить сборку.",
    },
    "pt": {
        # window
        "app_title": "Doutor de Logs do SMAPI",
        "btn_open": "Abrir log do SMAPI",
        "btn_export": "Exportar resumo",
        "status_ready": "Pronto. Abra um log do SMAPI para analisar.",
        "status_loaded": "Log carregado: {path}",
        "status_no_analysis": "Ainda não há análise. Abra um log primeiro.",
        "status_export_ok": "Resumo exportado para {path}",
        "status_export_fail": "Falha ao exportar resumo: {error}",

        # tabs
        "tab_overview": "Visão geral",
        "tab_mod_health": "Saúde dos mods",
        "tab_errors": "Erros",
        "tab_warnings": "Avisos",
        "tab_suggestions": "Sugestões",
        "tab_raw": "Log bruto",

        # overview
        "overview_title": "Visão geral de Stardew Valley / SMAPI",
        "overview_game_version": "Versão do jogo",
        "overview_smapi_version": "Versão do SMAPI",
        "overview_unknown": "Desconhecida",
        "overview_summary": "Resumo",
        "overview_mod_count": "Mods carregados: {count}",
        "overview_content_pack_count": "Content packs carregados: {count}",
        "overview_error_count": "Erros: {count}",
        "overview_warning_count": "Avisos: {count}",
        "overview_slow_start": "Tempo de inicialização: {seconds:.1f}s",
        "overview_hint": "Dica: corrija primeiro os erros, depois os avisos e só então os detalhes cosméticos/otimização.",

        # mod health
        "mod_health_title": "Saúde e risco dos mods",
        "mod_health_patched_header": "Mods que alteram o código do jogo (risco maior):",
        "mod_health_save_header": "Mods que mudam o serializador de salvamento (não remova no meio de um save):",
        "mod_health_console_header": "Mods com acesso direto ao console:",
        "mod_health_missing_dep_header": "Mods com dependências ausentes:",
        "mod_health_missing_dep_item": "{mod} → faltando: {missing}",
        "mod_health_none": "Nenhum mod claramente arriscado foi detectado neste log.",
        "mod_health_updates_header": "Mods com atualizações disponíveis:",
        "mod_health_update_item": "{name} {current} → {latest}",

        # errors
        "errors_header": "Erros encontrados neste log",
        "errors_none": "Nenhum erro do SMAPI foi encontrado. 🎉",
        "errors_intro": "Estes são os problemas mais importantes relatados pelo SMAPI:",

        # warnings
        "warnings_header": "Avisos",
        "warnings_none": "Nenhum aviso encontrado.",
        "warnings_intro": "Eles podem não quebrar o jogo na hora, mas valem a sua atenção:",

        # suggestions
        "suggestions_header": "Sugestões de correção",
        "suggestions_none": "Nenhuma sugestão automática por enquanto. Se o jogo ainda estiver estranho, confira as abas de Erros e Avisos.",

        # raw
        "raw_header": "Log completo do SMAPI",

        # generic issues
        "warn_rivatuner": "RivaTuner Statistics Server detectado. Ele pode causar crashes com o SMAPI; adicione uma exceção ou desative-o.",

        # suggestion types
        "sg.skipped_mod": "Corrija o mod {name}: o SMAPI pulou ele ({reason}). Abra a pasta do mod e verifique se o manifest.json é válido e se a versão é compatível com o seu jogo/SMAPI.",
        "sg.failed_mod": "Corrija o mod {name}: o SMAPI não conseguiu carregá-lo ({reason}). Veja as instruções de instalação na página do mod e reinstale se necessário.",
        "sg.missing_dep": "Instale a dependência obrigatória {missing} para o mod {mod}, ou desative o mod se não for usá-lo.",
        "sg.save_serializer": "{mod} altera a forma como o jogo salva. Faça backup dos saves e não remova esse mod no meio de um save.",
        "sg.patched_mods_many": "Você tem muitos mods alterando o código do jogo ({count}). Se aparecerem crashes estranhos, tente desativar utilidades/FX uma por vez.",
        "sg.rivatuner": "RivaTuner Statistics Server pode entrar em conflito com o SMAPI. Adicione uma exceção para Stardew Valley ou feche o programa enquanto joga.",
        "sg.updates": "{count} mod(s) podem ser atualizados. Manter frameworks e mods de base atualizados costuma resolver crashes e problemas invisíveis.",
        "sg.slow_start": "A inicialização do jogo levou cerca de {seconds:.1f}s. Muitos content packs e mods pesados aumentam o tempo de carregamento; se incomodar, considere enxugar um pouco a lista.",
    },
}


# =========================
# Data classes
# =========================

@dataclass
class SkippedMod:
    name: str
    reason: str


@dataclass
class MissingDependency:
    mod_name: str
    missing: str


@dataclass
class UpdateInfo:
    name: str
    latest: str
    current: str
    url: str


@dataclass
class SmapiAnalysis:
    game_version: Optional[str] = None
    smapi_version: Optional[str] = None
    mod_count: int = 0
    content_pack_count: int = 0
    skipped_mods: List[SkippedMod] = field(default_factory=list)
    failed_mods: List[SkippedMod] = field(default_factory=list)
    save_serializer_mods: List[str] = field(default_factory=list)
    patched_mods: List[str] = field(default_factory=list)
    direct_console_mods: List[str] = field(default_factory=list)
    missing_dependencies: List[MissingDependency] = field(default_factory=list)
    external_conflicts: List[str] = field(default_factory=list)
    update_infos: List[UpdateInfo] = field(default_factory=list)
    errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    slow_start_seconds: Optional[float] = None
    raw_log: str = ""


# =========================
# Parsing logic
# =========================

def _parse_time_to_seconds(time_str: str) -> Optional[float]:
    # format like 00:00:14.3893574
    try:
        parts = time_str.split(":")
        if len(parts) != 3:
            return None
        h = int(parts[0])
        m = int(parts[1])
        s = float(parts[2])
        return h * 3600 + m * 60 + s
    except Exception:
        return None


def analyze_smapi_log(text: str) -> SmapiAnalysis:
    analysis = SmapiAnalysis(raw_log=text)
    lines = text.splitlines()

    current_loading_mod: Optional[str] = None
    in_skipped_section = False
    in_save_serializer_section = False
    in_patched_section = False
    in_console_section = False

    for line in lines:
        # Versions
        if "SMAPI" in line and "with Stardew Valley" in line:
            m = re.search(r"SMAPI\s+([0-9.]+)\s+with Stardew Valley\s+([0-9.]+)", line)
            if m:
                analysis.smapi_version = m.group(1)
                analysis.game_version = m.group(2)

        # Counts
        if "Loaded" in line and "mods:" in line:
            m = re.search(r"Loaded\s+(\d+)\s+mods", line)
            if m:
                analysis.mod_count = int(m.group(1))
        if "Loaded" in line and "content packs:" in line:
            m = re.search(r"Loaded\s+(\d+)\s+content packs", line)
            if m:
                analysis.content_pack_count = int(m.group(1))

        # Startup time
        if "Instance_LoadContent() finished, elapsed =" in line:
            m = re.search(r"elapsed\s*=\s*'([^']+)'", line)
            if m:
                seconds = _parse_time_to_seconds(m.group(1))
                if seconds is not None:
                    analysis.slow_start_seconds = seconds

        # Track which mod is currently being loaded
        m_load = re.search(r"]\s+(.+?)\s+\(from\s+Mods", line)
        if m_load:
            current_loading_mod = m_load.group(1)

        # "Failed:" lines (TRACE section)
        if "Failed:" in line:
            reason = line.split("Failed:", 1)[1].strip()
            if current_loading_mod:
                analysis.failed_mods.append(SkippedMod(current_loading_mod, reason))
                # Missing dependency info
                if "requires mods which aren't installed" in reason:
                    m_dep = re.search(r"\(([^)]+)\)", reason)
                    if m_dep:
                        missing = m_dep.group(1)
                        analysis.missing_dependencies.append(
                            MissingDependency(current_loading_mod, missing)
                        )

        # Skipped mods header
        if "Skipped mods" in line:
            in_skipped_section = True
            continue

        if in_skipped_section:
            if "- " in line:
                m = re.search(r"]\s+-\s+(.+?)\s+because\s+(.+)$", line)
                if m:
                    name = m.group(1).strip()
                    reason = m.group(2).strip()
                    analysis.skipped_mods.append(SkippedMod(name, reason))
                    if "requires mods which aren't installed" in reason:
                        m_dep = re.search(r"\(([^)]+)\)", reason)
                        if m_dep:
                            analysis.missing_dependencies.append(
                                MissingDependency(name, m_dep.group(1))
                            )
            elif line.strip() == "" or "These mods could not be added" in line:
                # stay in section
                pass
            else:
                in_skipped_section = False

        # Save serializer section
        if "Changed save serializer" in line:
            in_save_serializer_section = True
            continue
        if in_save_serializer_section:
            if "- " in line:
                m = re.search(r"-\s+(.+)$", line)
                if m:
                    analysis.save_serializer_mods.append(m.group(1).strip())
            elif line.strip() == "" or "These mods change the save serializer" in line:
                pass
            else:
                in_save_serializer_section = False

        # Patched game code section
        if "Patched game code" in line:
            in_patched_section = True
            continue
        if in_patched_section:
            if "- " in line:
                m = re.search(r"-\s+(.+)$", line)
                if m:
                    analysis.patched_mods.append(m.group(1).strip())
            elif line.strip() == "" or "These mods directly change the game code" in line:
                pass
            else:
                in_patched_section = False

        # Direct console access
        if "Direct console access" in line:
            in_console_section = True
            continue
        if in_console_section:
            if "- " in line:
                m = re.search(r"-\s+(.+)$", line)
                if m:
                    analysis.direct_console_mods.append(m.group(1).strip())
            elif line.strip() == "" or "These mods access the SMAPI console window" in line:
                pass
            else:
                in_console_section = False

        # External conflicts (RivaTuner etc.)
        if "RivaTuner Statistics Server" in line:
            analysis.external_conflicts.append("RivaTuner Statistics Server")

        # Generic SMAPI [ERROR]/[WARN] lines
        if "ERROR SMAPI" in line and "Skipped mods" not in line:
            msg = re.sub(r"^\[.*?\]\s*", "", line).strip()
            if msg:
                analysis.errors.append(msg)
        if "WARN  SMAPI" in line and "Changed save serializer" not in line:
            msg = re.sub(r"^\[.*?\]\s*", "", line).strip()
            if msg:
                analysis.warnings.append(msg)

        # Update infos (alert details)
        if "ALERT SMAPI" in line and "You can update" not in line:
            m = re.search(r"]\s+(.+?)\s+([0-9.]+):\s+(\S+)\s+\(you have\s+([0-9.]+)\)", line)
            if m:
                name = m.group(1).strip()
                latest = m.group(2).strip()
                url = m.group(3).strip()
                current = m.group(4).strip()
                analysis.update_infos.append(
                    UpdateInfo(name=name, latest=latest, current=current, url=url)
                )

    return analysis


# =========================
# Suggestions builder
# =========================

def build_suggestions(analysis: SmapiAnalysis, lang: str) -> List[str]:
    t = lambda key, **kw: TEXT[lang][key].format(**kw)
    suggestions: List[str] = []

    # Skipped mods
    for sm in analysis.skipped_mods:
        suggestions.append(t("sg.skipped_mod", name=sm.name, reason=sm.reason))

    # Failed mods
    for fm in analysis.failed_mods:
        suggestions.append(t("sg.failed_mod", name=fm.name, reason=fm.reason))

    # Missing dependencies
    for dep in analysis.missing_dependencies:
        suggestions.append(t("sg.missing_dep", mod=dep.mod_name, missing=dep.missing))

    # Save serializer
    for mname in analysis.save_serializer_mods:
        suggestions.append(t("sg.save_serializer", mod=mname))

    # Many patched mods
    if len(analysis.patched_mods) >= 15:
        suggestions.append(t("sg.patched_mods_many", count=len(analysis.patched_mods)))

    # External conflicts
    if any("RivaTuner" in x for x in analysis.external_conflicts):
        suggestions.append(t("sg.rivatuner"))

    # Updates
    if analysis.update_infos:
        suggestions.append(t("sg.updates", count=len(analysis.update_infos)))

    # Slow startup
    if analysis.slow_start_seconds and analysis.slow_start_seconds > 20:
        suggestions.append(t("sg.slow_start", seconds=analysis.slow_start_seconds))

    return suggestions


# =========================
# Helpers: SMAPI dir + config
# =========================

def detect_smapi_log_dir() -> Optional[str]:
    """
    Try to auto-detect the SMAPI ErrorLogs folder.
    Windows: %APPDATA%\\StardewValley\\ErrorLogs
    Linux:   ~/.local/share/StardewValley/ErrorLogs
    macOS:   ~/Library/Application Support/StardewValley/ErrorLogs
    """
    candidates: List[str] = []

    if os.name == "nt":
        appdata = os.getenv("APPDATA")
        if appdata:
            candidates.append(os.path.join(appdata, "StardewValley", "ErrorLogs"))
    else:
        home = os.path.expanduser("~")
        candidates.append(
            os.path.join(home, "Library", "Application Support", "StardewValley", "ErrorLogs")
        )
        candidates.append(
            os.path.join(home, ".local", "share", "StardewValley", "ErrorLogs")
        )

    for path in candidates:
        if os.path.isdir(path):
            return path

    return None


# =========================
# Tkinter UI app
# =========================

class SmapiLogDoctorApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.lang = "en"
        self.analysis: Optional[SmapiAnalysis] = None
        self.current_path: Optional[str] = None

        # remember last folder + language
        self.config_path = self._compute_config_path()
        self.last_dir: Optional[str] = None
        self._load_config()

        # language dropdown options: (code, label)
        self.lang_options = [
            ("en", "EN"),
            ("zh", "中文"),
            ("ru", "RU"),
            ("pt", "PT"),
        ]
        self.lang_var = tk.StringVar()

        self.root.title(TEXT[self.lang]["app_title"])
        self.root.geometry("1000x700")

        self._build_ui()

    # ---------- Config helpers ----------

    def _compute_config_path(self) -> str:
        base_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
        return os.path.join(base_dir, "smapi_log_doctor_config.json")

    def _load_config(self) -> None:
        try:
            if os.path.isfile(self.config_path):
                with open(self.config_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                lang = data.get("lang")
                if lang in TEXT:
                    self.lang = lang
                last_dir = data.get("last_dir")
                if last_dir and os.path.isdir(last_dir):
                    self.last_dir = last_dir
        except Exception:
            # ignore config errors, fall back to defaults
            pass

    def _save_config(self) -> None:
        data = {
            "lang": self.lang,
            "last_dir": self.last_dir,
        }
        try:
            with open(self.config_path, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
        except Exception:
            # don't crash app on save failure
            pass

    # ---------- Translation helper ----------

    def _t(self, key: str, **kwargs) -> str:
        return TEXT[self.lang][key].format(**kwargs)

    # ---------- UI building ----------

    def _build_ui(self) -> None:
        # Top toolbar
        toolbar = ttk.Frame(self.root)
        toolbar.pack(side="top", fill="x", padx=4, pady=4)

        self.btn_open = ttk.Button(toolbar, text=self._t("btn_open"), command=self.open_log)
        self.btn_open.pack(side="left")

        self.btn_export = ttk.Button(toolbar, text=self._t("btn_export"), command=self.export_summary)
        self.btn_export.pack(side="left", padx=(4, 0))

        # Language dropdown (right side)
        lang_frame = ttk.Frame(toolbar)
        lang_frame.pack(side="right")

        lang_label = ttk.Label(lang_frame, text="Language:")
        lang_label.pack(side="left", padx=(0, 4))

        # set initial dropdown label from current lang code
        initial_label = next(
            (label for code, label in self.lang_options if code == self.lang),
            "EN",
        )
        self.lang_var.set(initial_label)

        self.lang_combobox = ttk.Combobox(
            lang_frame,
            textvariable=self.lang_var,
            state="readonly",
            values=[label for _, label in self.lang_options],
            width=6,
        )
        self.lang_combobox.pack(side="left")
        self.lang_combobox.bind("<<ComboboxSelected>>", self._on_lang_selected)

        # Notebook tabs
        self.notebook = ttk.Notebook(self.root)
        self.notebook.pack(fill="both", expand=True, padx=4, pady=4)

        self.overview_text = self._create_text_tab("tab_overview")
        self.mod_health_text = self._create_text_tab("tab_mod_health")
        self.errors_text = self._create_text_tab("tab_errors")
        self.warnings_text = self._create_text_tab("tab_warnings")
        self.suggestions_text = self._create_text_tab("tab_suggestions")
        self.raw_log_text = self._create_text_tab("tab_raw")

        # Status bar
        self.status_var = tk.StringVar(value=self._t("status_ready"))
        status_bar = ttk.Label(self.root, textvariable=self.status_var, anchor="w")
        status_bar.pack(side="bottom", fill="x")

    def _create_text_tab(self, title_key: str) -> tk.Text:
        frame = ttk.Frame(self.notebook)
        self.notebook.add(frame, text=self._t(title_key))

        text = tk.Text(
            frame,
            wrap="word",
            font=("Consolas", 10),
            undo=False,
        )
        text.pack(fill="both", expand=True)
        self._configure_text_tags(text)
        text.config(state="disabled")
        return text

    def _configure_text_tags(self, text: tk.Text) -> None:
        text.tag_configure(
            "header",
            font=("Consolas", 11, "bold"),
            spacing3=6,
        )
        text.tag_configure(
            "subheader",
            font=("Consolas", 10, "bold"),
            spacing3=4,
        )
        text.tag_configure(
            "error",
            foreground="#d22",
        )
        text.tag_configure(
            "warning",
            foreground="#b36b00",
        )
        text.tag_configure(
            "info",
            foreground="#005caa",
        )
        text.tag_configure(
            "bullet",
            lmargin1=20,
            lmargin2=20,
        )
        text.tag_configure(
            "muted",
            foreground="#666666",
        )
        text.tag_configure(
            "emphasis",
            font=("Consolas", 10, "italic"),
        )

    # ---------- Language dropdown logic ----------

    def _on_lang_selected(self, event=None) -> None:
        label = self.lang_var.get()
        for code, lbl in self.lang_options:
            if lbl == label:
                self.set_language(code)
                break

    def set_language(self, lang: str) -> None:
        if lang == self.lang:
            return
        self.lang = lang
        self.root.title(TEXT[self.lang]["app_title"])
        # Update button labels & tab titles
        self.btn_open.config(text=self._t("btn_open"))
        self.btn_export.config(text=self._t("btn_export"))

        # Update dropdown label if needed
        if hasattr(self, "lang_var"):
            label = next((lbl for code, lbl in self.lang_options if code == self.lang), "EN")
            self.lang_var.set(label)

        # Re-label tabs
        for tab, key in zip(
            self.notebook.tabs(),
            [
                "tab_overview",
                "tab_mod_health",
                "tab_errors",
                "tab_warnings",
                "tab_suggestions",
                "tab_raw",
            ],
        ):
            self.notebook.tab(tab, text=self._t(key))

        # Rerender content
        if self.analysis:
            self.render_all()
            if self.current_path:
                self.status_var.set(self._t("status_loaded", path=self.current_path))
        else:
            self.status_var.set(self._t("status_ready"))

        # remember language
        self._save_config()

    # ---------- File handling ----------

    def _get_initial_open_dir(self) -> str:
        # 1) last folder if still exists
        if self.last_dir and os.path.isdir(self.last_dir):
            return self.last_dir

        # 2) auto-detected SMAPI ErrorLogs folder
        detected = detect_smapi_log_dir()
        if detected:
            return detected

        # 3) fallback: home directory
        return os.path.expanduser("~")

    def open_log(self) -> None:
        initial_dir = self._get_initial_open_dir()
        path = filedialog.askopenfilename(
            title="Select SMAPI log",
            filetypes=[
                ("Text files", "*.txt"),
                ("All files", "*.*"),
            ],
            initialdir=initial_dir,
        )
        if not path:
            return
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as f:
                text = f.read()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to read file:\n{e}")
            return

        try:
            self.analysis = analyze_smapi_log(text)
        except Exception as e:
            messagebox.showerror("Error", f"Failed to analyze log:\n{e}")
            return

        self.current_path = path
        # remember folder for next time
        self.last_dir = os.path.dirname(path)
        self._save_config()

        self.render_all()
        self.status_var.set(self._t("status_loaded", path=path))

    def export_summary(self) -> None:
        if not self.analysis:
            messagebox.showinfo("Info", self._t("status_no_analysis"))
            return
        path = filedialog.asksaveasfilename(
            title="Export summary",
            defaultextension=".txt",
            filetypes=[("Text files", "*.txt")],
        )
        if not path:
            return

        try:
            summary_text = self._build_plain_summary()
            with open(path, "w", encoding="utf-8") as f:
                f.write(summary_text)
            self.status_var.set(self._t("status_export_ok", path=path))
        except Exception as e:
            self.status_var.set(self._t("status_export_fail", error=e))

    # ---------- Rendering ----------

    def _clear_and_enable(self, text: tk.Text) -> None:
        text.config(state="normal")
        text.delete("1.0", tk.END)

    def render_all(self) -> None:
        if not self.analysis:
            return
        self._render_overview()
        self._render_mod_health()
        self._render_errors()
        self._render_warnings()
        self._render_suggestions()
        self._render_raw()

    def _render_overview(self) -> None:
        a = self.analysis
        t = self._t
        text = self.overview_text
        self._clear_and_enable(text)

        text.insert(tk.END, t("overview_title") + "\n", ("header",))

        # Versions
        text.insert(
            tk.END,
            f"{t('overview_game_version')}: {a.game_version or t('overview_unknown')}\n",
            ("info",),
        )
        text.insert(
            tk.END,
            f"{t('overview_smapi_version')}: {a.smapi_version or t('overview_unknown')}\n\n",
            ("info",),
        )

        # Summary
        text.insert(tk.END, t("overview_summary") + "\n", ("subheader",))

        text.insert(
            tk.END,
            "• " + t("overview_mod_count", count=a.mod_count) + "\n",
            ("bullet",),
        )
        text.insert(
            tk.END,
            "• " + t("overview_content_pack_count", count=a.content_pack_count) + "\n",
            ("bullet",),
        )
        text.insert(
            tk.END,
            "• " + t("overview_error_count", count=len(a.errors)) + "\n",
            ("bullet", "error") if a.errors else ("bullet",),
        )
        text.insert(
            tk.END,
            "• " + t("overview_warning_count", count=len(a.warnings)) + "\n",
            ("bullet", "warning") if a.warnings else ("bullet",),
        )
        if a.slow_start_seconds is not None:
            text.insert(
                tk.END,
                "• " + t("overview_slow_start", seconds=a.slow_start_seconds) + "\n",
                ("bullet", "muted"),
            )

        text.insert(tk.END, "\n" + t("overview_hint") + "\n", ("muted",))

        text.config(state="disabled")

    def _render_mod_health(self) -> None:
        a = self.analysis
        t = self._t
        text = self.mod_health_text
        self._clear_and_enable(text)

        text.insert(tk.END, t("mod_health_title") + "\n", ("header",))

        sections_written = False

        # Patched game code
        if a.patched_mods:
            sections_written = True
            text.insert(
                tk.END, "\n" + t("mod_health_patched_header") + "\n", ("subheader",)
            )
            for m in a.patched_mods:
                text.insert(
                    tk.END,
                    "• " + m + "\n",
                    ("bullet", "warning"),
                )

        # Save serializer
        if a.save_serializer_mods:
            sections_written = True
            text.insert(
                tk.END, "\n" + t("mod_health_save_header") + "\n", ("subheader",)
            )
            for m in a.save_serializer_mods:
                text.insert(
                    tk.END,
                    "• " + m + "\n",
                    ("bullet", "error"),
                )

        # Direct console access
        if a.direct_console_mods:
            sections_written = True
            text.insert(
                tk.END,
                "\n" + t("mod_health_console_header") + "\n",
                ("subheader",),
            )
            for m in a.direct_console_mods:
                text.insert(
                    tk.END,
                    "• " + m + "\n",
                    ("bullet", "muted"),
                )

        # Missing dependencies
        if a.missing_dependencies:
            sections_written = True
            text.insert(
                tk.END,
                "\n" + t("mod_health_missing_dep_header") + "\n",
                ("subheader",),
            )
            for dep in a.missing_dependencies:
                text.insert(
                    tk.END,
                    "• "
                    + t(
                        "mod_health_missing_dep_item",
                        mod=dep.mod_name,
                        missing=dep.missing,
                    )
                    + "\n",
                    ("bullet", "error"),
                )

        # Updates
        if a.update_infos:
            sections_written = True
            text.insert(
                tk.END,
                "\n" + t("mod_health_updates_header") + "\n",
                ("subheader",),
            )
            for u in a.update_infos:
                text.insert(
                    tk.END,
                    "• "
                    + t(
                        "mod_health_update_item",
                        name=u.name,
                        current=u.current,
                        latest=u.latest,
                    )
                    + "\n",
                    ("bullet", "info"),
                )

        if not sections_written:
            text.insert(tk.END, "\n" + t("mod_health_none") + "\n", ("muted",))

        text.config(state="disabled")

    def _render_errors(self) -> None:
        a = self.analysis
        t = self._t
        text = self.errors_text
        self._clear_and_enable(text)

        text.insert(tk.END, t("errors_header") + "\n", ("header",))

        if not a.errors and not a.skipped_mods and not a.failed_mods:
            text.insert(tk.END, t("errors_none") + "\n", ("info",))
            text.config(state="disabled")
            return

        text.insert(tk.END, t("errors_intro") + "\n\n", ("muted",))

        # Skipped / failed mods as "hard errors"
        for sm in a.skipped_mods:
            text.insert(
                tk.END,
                f"• [Skipped] {sm.name} — {sm.reason}\n",
                ("bullet", "error"),
            )
        for fm in a.failed_mods:
            text.insert(
                tk.END,
                f"• [Failed] {fm.name} — {fm.reason}\n",
                ("bullet", "error"),
            )

        # Raw ERROR lines
        for e in a.errors:
            text.insert(
                tk.END,
                "• " + e + "\n",
                ("bullet", "error"),
            )

        text.config(state="disabled")

    def _render_warnings(self) -> None:
        a = self.analysis
        t = self._t
        text = self.warnings_text
        self._clear_and_enable(text)

        text.insert(tk.END, t("warnings_header") + "\n", ("header",))

        if not a.warnings and not a.external_conflicts:
            text.insert(tk.END, t("warnings_none") + "\n", ("info",))
            text.config(state="disabled")
            return

        text.insert(tk.END, t("warnings_intro") + "\n\n", ("muted",))

        for w in a.warnings:
            text.insert(
                tk.END,
                "• " + w + "\n",
                ("bullet", "warning"),
            )

        # External conflicts like RivaTuner
        for x in a.external_conflicts:
            if "RivaTuner" in x:
                text.insert(
                    tk.END,
                    "• " + TEXT[self.lang]["warn_rivatuner"] + "\n",
                    ("bullet", "warning"),
                )

        text.config(state="disabled")

    def _render_suggestions(self) -> None:
        a = self.analysis
        text = self.suggestions_text
        self._clear_and_enable(text)

        t = self._t
        text.insert(tk.END, t("suggestions_header") + "\n", ("header",))

        suggestions = build_suggestions(a, self.lang)
        if not suggestions:
            text.insert(tk.END, t("suggestions_none") + "\n", ("info",))
            text.config(state="disabled")
            return

        for s in suggestions:
            # Light severity coloring heuristic
            tags = ["bullet"]
            if ("save" in s.lower() or "存档" in s or "сейв" in s.lower() or "salva" in s.lower()):
                tags.append("error")
            elif ("update" in s.lower() or "更新" in s or "обнов" in s.lower() or "atualiz" in s.lower()):
                tags.append("info")
            elif "RivaTuner" in s:
                tags.append("warning")

            text.insert(tk.END, "• " + s + "\n\n", tuple(tags))

        text.config(state="disabled")

    def _render_raw(self) -> None:
        a = self.analysis
        t = self._t
        text = self.raw_log_text
        self._clear_and_enable(text)

        text.insert(tk.END, t("raw_header") + "\n\n", ("header",))
        text.insert(tk.END, a.raw_log)
        text.config(state="disabled")

    # ---------- Export summary (plain text) ----------

    def _build_plain_summary(self) -> str:
        if not self.analysis:
            return ""
        a = self.analysis
        t = self._t

        parts: List[str] = []

        parts.append(t("overview_title"))
        parts.append("=" * 60)
        parts.append(f"{t('overview_game_version')}: {a.game_version or t('overview_unknown')}")
        parts.append(f"{t('overview_smapi_version')}: {a.smapi_version or t('overview_unknown')}")
        parts.append(t("overview_mod_count", count=a.mod_count))
        parts.append(t("overview_content_pack_count", count=a.content_pack_count))
        if a.slow_start_seconds is not None:
            parts.append(t("overview_slow_start", seconds=a.slow_start_seconds))
        parts.append("")

        # Errors
        parts.append(t("errors_header"))
        parts.append("-" * 60)
        if not a.errors and not a.skipped_mods and not a.failed_mods:
            parts.append(t("errors_none"))
        else:
            for sm in a.skipped_mods:
                parts.append(f"[Skipped] {sm.name} — {sm.reason}")
            for fm in a.failed_mods:
                parts.append(f"[Failed] {fm.name} — {fm.reason}")
            for e in a.errors:
                parts.append(e)
        parts.append("")

        # Warnings
        parts.append(t("warnings_header"))
        parts.append("-" * 60)
        if not a.warnings and not a.external_conflicts:
            parts.append(t("warnings_none"))
        else:
            for w in a.warnings:
                parts.append(w)
            for x in a.external_conflicts:
                if "RivaTuner" in x:
                    parts.append(TEXT[self.lang]["warn_rivatuner"])
        parts.append("")

        # Mod health
        parts.append(t("mod_health_title"))
        parts.append("-" * 60)

        if a.patched_mods:
            parts.append(t("mod_health_patched_header"))
            for m in a.patched_mods:
                parts.append("  - " + m)
        if a.save_serializer_mods:
            parts.append(t("mod_health_save_header"))
            for m in a.save_serializer_mods:
                parts.append("  - " + m)
        if a.direct_console_mods:
            parts.append(t("mod_health_console_header"))
            for m in a.direct_console_mods:
                parts.append("  - " + m)
        if a.missing_dependencies:
            parts.append(t("mod_health_missing_dep_header"))
            for dep in a.missing_dependencies:
                parts.append(
                    "  - "
                    + t(
                        "mod_health_missing_dep_item",
                        mod=dep.mod_name,
                        missing=dep.missing,
                    )
                )
        if a.update_infos:
            parts.append(t("mod_health_updates_header"))
            for u in a.update_infos:
                parts.append(
                    "  - "
                    + t(
                        "mod_health_update_item",
                        name=u.name,
                        current=u.current,
                        latest=u.latest,
                    )
                )

        if (
            not a.patched_mods
            and not a.save_serializer_mods
            and not a.direct_console_mods
            and not a.missing_dependencies
            and not a.update_infos
        ):
            parts.append(t("mod_health_none"))
        parts.append("")

        # Suggestions
        parts.append(t("suggestions_header"))
        parts.append("-" * 60)
        suggestions = build_suggestions(a, self.lang)
        if not suggestions:
            parts.append(t("suggestions_none"))
        else:
            for s in suggestions:
                parts.append(" - " + s)
        parts.append("")

        return "\n".join(parts)


# =========================
# Main entry
# =========================

def main() -> None:
    root = tk.Tk()
    app = SmapiLogDoctorApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
