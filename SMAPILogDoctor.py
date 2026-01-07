import os
import re
import json
import html
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
        "btn_export": "Export Summary (TXT)",
        "btn_export_html": "Export Summary (HTML)",
        "status_ready": "Ready. Open a SMAPI log to analyze.",
        "status_loaded": "Loaded log: {path}",
        "status_no_analysis": "No analysis yet. Open a log first.",
        "status_export_ok": "Summary exported to {path}",
        "status_export_fail": "Failed to export summary: {error}",
        "status_export_html_ok": "HTML report exported to {path}",
        "status_export_html_fail": "Failed to export HTML report: {error}",

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
        "overview_hint": "Tip: fix errors first, then warnings, then cosmetic / consistency issues.",

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
        "errors_none": "No SMAPI/game errors detected. 🎉",
        "errors_intro": "These are the most important issues reported by SMAPI or the game:",

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
        "btn_export": "导出文本报告",
        "btn_export_html": "导出 HTML 报告",
        "status_ready": "就绪。先打开一份 SMAPI 日志再分析。",
        "status_loaded": "已加载日志：{path}",
        "status_no_analysis": "还没有分析结果，请先打开一份日志。",
        "status_export_ok": "已导出文本总结到 {path}",
        "status_export_fail": "导出文本总结失败：{error}",
        "status_export_html_ok": "已导出 HTML 报告到 {path}",
        "status_export_html_fail": "导出 HTML 报告失败：{error}",

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
        "errors_none": "未检测到 SMAPI / 游戏错误。🎉",
        "errors_intro": "下面是 SMAPI 或游戏本身报告的关键问题：",

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
    "es": {
        "app_title": "Doctor de Registros SMAPI",
        "btn_open": "Abrir registro SMAPI",
        "btn_export": "Exportar resumen (TXT)",
        "btn_export_html": "Exportar resumen (HTML)",
        "status_ready": "Listo. Abre un registro SMAPI para analizar.",
        "status_loaded": "Registro cargado: {path}",
        "status_no_analysis": "Sin análisis. Abre un registro primero.",
        "status_export_ok": "Resumen exportado a {path}",
        "status_export_fail": "Error al exportar resumen: {error}",
        "status_export_html_ok": "Informe HTML exportado a {path}",
        "status_export_html_fail": "Error al exportar informe HTML: {error}",
        "tab_overview": "Resumen",
        "tab_mod_health": "Salud de Mods",
        "tab_errors": "Errores",
        "tab_warnings": "Advertencias",
        "tab_suggestions": "Sugerencias",
        "tab_raw": "Registro sin procesar",
        "overview_title": "Resumen de Stardew Valley / SMAPI",
        "overview_game_version": "Versión del juego",
        "overview_smapi_version": "Versión de SMAPI",
        "overview_unknown": "Desconocido",
        "overview_summary": "Sumario",
        "overview_mod_count": "Mods cargados: {count}",
        "overview_content_pack_count": "Paquetes de contenido cargados: {count}",
        "overview_error_count": "Errores: {count}",
        "overview_warning_count": "Advertencias: {count}",
        "overview_slow_start": "Tiempo de inicio: {seconds:.1f}s",
        "overview_hint": "Consejo: arregla primero los errores, luego las advertencias y después los problemas cosméticos.",
        "mod_health_title": "Salud y Riesgo de Mods",
        "mod_health_patched_header": "Mods que modifican código del juego (mayor riesgo):",
        "mod_health_save_header": "Mods que cambian el guardado (NO quitar a mitad de partida):",
        "mod_health_console_header": "Mods con acceso directo a la consola:",
        "mod_health_missing_dep_header": "Mods con dependencias faltantes:",
        "mod_health_missing_dep_item": "{mod} → falta: {missing}",
        "mod_health_none": "No se detectaron mods riesgosos en este registro.",
        "mod_health_updates_header": "Mods con actualizaciones disponibles:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Errores encontrados en este registro",
        "errors_none": "No se detectaron errores de SMAPI/juego. 🎉",
        "errors_intro": "Estos son los problemas más importantes reportados por SMAPI o el juego:",
        "warnings_header": "Advertencias",
        "warnings_none": "No se encontraron advertencias.",
        "warnings_intro": "Puede que no rompan tu juego inmediatamente, pero vale la pena revisarlos:",
        "suggestions_header": "Arreglos sugeridos",
        "suggestions_none": "Sin sugerencias automáticas. Si el juego falla, revisa las pestañas de Errores/Advertencias.",
        "raw_header": "Registro SMAPI Completo",
        "warn_rivatuner": "RivaTuner Statistics Server detectado. Puede causar cierres con SMAPI; añade una excepción o desactívalo.",
        "sg.skipped_mod": "Arreglar mod \"{name}\": SMAPI lo omitió ({reason}). Revisa su carpeta y asegura que tenga un manifest.json válido.",
        "sg.failed_mod": "Arreglar mod \"{name}\": SMAPI no pudo cargarlo ({reason}). Revisa las instrucciones de instalación.",
        "sg.missing_dep": "Instala la dependencia requerida \"{missing}\" para \"{mod}\", o desactiva el mod si no lo necesitas.",
        "sg.save_serializer": "\"{mod}\" cambia el serializador de guardado. Haz copia de seguridad y evita quitar este mod a mitad de partida.",
        "sg.patched_mods_many": "Tienes muchos mods modificando código del juego ({count}). Si ves cierres extraños, prueba desactivar mods de utilidad.",
        "sg.rivatuner": "RivaTuner Statistics Server puede entrar en conflicto con SMAPI. Añade una excepción para Stardew Valley.",
        "sg.updates": "Puedes actualizar {count} mods. Mantener los frameworks actualizados suele arreglar cierres.",
        "sg.slow_start": "El inicio del juego tomó {seconds:.1f}s. Grandes paquetes de contenido pueden aumentar el tiempo de carga.",
    },
    "fr": {
        "app_title": "Docteur de Logs SMAPI",
        "btn_open": "Ouvrir un log SMAPI",
        "btn_export": "Exporter résumé (TXT)",
        "btn_export_html": "Exporter résumé (HTML)",
        "status_ready": "Prêt. Ouvrez un log SMAPI pour analyser.",
        "status_loaded": "Log chargé : {path}",
        "status_no_analysis": "Aucune analyse. Ouvrez d'abord un log.",
        "status_export_ok": "Résumé exporté vers {path}",
        "status_export_fail": "Échec de l'exportation : {error}",
        "status_export_html_ok": "Rapport HTML exporté vers {path}",
        "status_export_html_fail": "Échec du rapport HTML : {error}",
        "tab_overview": "Aperçu",
        "tab_mod_health": "Santé des Mods",
        "tab_errors": "Erreurs",
        "tab_warnings": "Avertissements",
        "tab_suggestions": "Suggestions",
        "tab_raw": "Log brut",
        "overview_title": "Aperçu Stardew Valley / SMAPI",
        "overview_game_version": "Version du jeu",
        "overview_smapi_version": "Version SMAPI",
        "overview_unknown": "Inconnu",
        "overview_summary": "Résumé",
        "overview_mod_count": "Mods chargés : {count}",
        "overview_content_pack_count": "Packs de contenu chargés : {count}",
        "overview_error_count": "Erreurs : {count}",
        "overview_warning_count": "Avertissements : {count}",
        "overview_slow_start": "Temps de démarrage : {seconds:.1f}s",
        "overview_hint": "Conseil : corrigez d'abord les erreurs, puis les avertissements.",
        "mod_health_title": "Santé et Risques des Mods",
        "mod_health_patched_header": "Mods modifiant le code du jeu (risque élevé) :",
        "mod_health_save_header": "Mods modifiant la sauvegarde (ne pas retirer en cours de partie) :",
        "mod_health_console_header": "Mods avec accès direct à la console :",
        "mod_health_missing_dep_header": "Mods avec dépendances manquantes :",
        "mod_health_missing_dep_item": "{mod} → manque : {missing}",
        "mod_health_none": "Aucun mod à risque détecté.",
        "mod_health_updates_header": "Mises à jour disponibles :",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Erreurs trouvées",
        "errors_none": "Aucune erreur SMAPI/jeu détectée. 🎉",
        "errors_intro": "Voici les problèmes les plus importants signalés :",
        "warnings_header": "Avertissements",
        "warnings_none": "Aucun avertissement trouvé.",
        "warnings_intro": "Ils ne cassent pas forcément le jeu, mais vérifiez-les :",
        "suggestions_header": "Correctifs suggérés",
        "suggestions_none": "Aucune suggestion automatique. Vérifiez les onglets Erreurs/Avertissements.",
        "raw_header": "Log SMAPI complet",
        "warn_rivatuner": "RivaTuner Statistics Server détecté. Il peut causer des plantages avec SMAPI.",
        "sg.skipped_mod": "Corriger \"{name}\" : SMAPI l'a ignoré ({reason}). Vérifiez le dossier et le manifest.json.",
        "sg.failed_mod": "Corriger \"{name}\" : Échec du chargement ({reason}). Vérifiez les instructions d'installation.",
        "sg.missing_dep": "Installez la dépendance \"{missing}\" pour \"{mod}\", ou désactivez le mod.",
        "sg.save_serializer": "\"{mod}\" modifie le format de sauvegarde. Faites des backups et ne le retirez pas en cours de partie.",
        "sg.patched_mods_many": "Beaucoup de mods modifient le code du jeu ({count}). En cas de bug, désactivez les mods utilitaires.",
        "sg.rivatuner": "RivaTuner Statistics Server peut entrer en conflit avec SMAPI. Ajoutez une exception.",
        "sg.updates": "Vous pouvez mettre à jour {count} mods. Les mises à jour corrigent souvent les plantages.",
        "sg.slow_start": "Le démarrage a pris {seconds:.1f}s. Les gros packs de contenu peuvent ralentir le chargement.",
    },
    "de": {
        "app_title": "SMAPI Log Doktor",
        "btn_open": "SMAPI-Log öffnen",
        "btn_export": "Zusammenfassung exportieren (TXT)",
        "btn_export_html": "Zusammenfassung exportieren (HTML)",
        "status_ready": "Bereit. Öffne ein SMAPI-Log zur Analyse.",
        "status_loaded": "Log geladen: {path}",
        "status_no_analysis": "Noch keine Analyse. Öffne zuerst ein Log.",
        "status_export_ok": "Zusammenfassung exportiert nach {path}",
        "status_export_fail": "Export fehlgeschlagen: {error}",
        "status_export_html_ok": "HTML-Bericht exportiert nach {path}",
        "status_export_html_fail": "HTML-Export fehlgeschlagen: {error}",
        "tab_overview": "Übersicht",
        "tab_mod_health": "Mod-Gesundheit",
        "tab_errors": "Fehler",
        "tab_warnings": "Warnungen",
        "tab_suggestions": "Vorschläge",
        "tab_raw": "Rohes Log",
        "overview_title": "Stardew Valley / SMAPI Übersicht",
        "overview_game_version": "Spielversion",
        "overview_smapi_version": "SMAPI-Version",
        "overview_unknown": "Unbekannt",
        "overview_summary": "Zusammenfassung",
        "overview_mod_count": "Geladene Mods: {count}",
        "overview_content_pack_count": "Geladene Content Packs: {count}",
        "overview_error_count": "Fehler: {count}",
        "overview_warning_count": "Warnungen: {count}",
        "overview_slow_start": "Startzeit: {seconds:.1f}s",
        "overview_hint": "Tipp: Zuerst Fehler beheben, dann Warnungen.",
        "mod_health_title": "Mod-Gesundheit & Risiko",
        "mod_health_patched_header": "Mods, die Spielcode patchen (höheres Risiko):",
        "mod_health_save_header": "Mods, die das Speicherformat ändern (NICHT während eines Durchlaufs entfernen):",
        "mod_health_console_header": "Mods mit direktem Konsolenzugriff:",
        "mod_health_missing_dep_header": "Mods mit fehlenden Abhängigkeiten:",
        "mod_health_missing_dep_item": "{mod} → fehlt: {missing}",
        "mod_health_none": "Keine riskanten Mods erkannt.",
        "mod_health_updates_header": "Verfügbare Updates:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Gefundene Fehler",
        "errors_none": "Keine SMAPI/Spiel-Fehler erkannt. 🎉",
        "errors_intro": "Dies sind die wichtigsten Probleme laut SMAPI:",
        "warnings_header": "Warnungen",
        "warnings_none": "Keine Warnungen gefunden.",
        "warnings_intro": "Diese brechen das Spiel vielleicht nicht sofort, sind aber wichtig:",
        "suggestions_header": "Vorgeschlagene Lösungen",
        "suggestions_none": "Keine automatischen Vorschläge. Prüfe die Fehler/Warnungen-Tabs.",
        "raw_header": "Vollständiges SMAPI-Log",
        "warn_rivatuner": "RivaTuner Statistics Server erkannt. Kann Abstürze verursachen.",
        "sg.skipped_mod": "Fix Mod \"{name}\": SMAPI hat ihn übersprungen ({reason}). Prüfe manifest.json.",
        "sg.failed_mod": "Fix Mod \"{name}\": Konnte nicht geladen werden ({reason}). Prüfe Installationsanleitung.",
        "sg.missing_dep": "Installiere benötigte Abhängigkeit \"{missing}\" für \"{mod}\".",
        "sg.save_serializer": "\"{mod}\" ändert das Speicherformat. Mache Backups und entferne ihn nicht mittendrin.",
        "sg.patched_mods_many": "Viele Mods ändern Spielcode ({count}). Bei Abstürzen Utility-Mods deaktivieren.",
        "sg.rivatuner": "RivaTuner Statistics Server kann Konflikte verursachen. Füge eine Ausnahme hinzu.",
        "sg.updates": "Du kannst {count} Mods aktualisieren. Updates beheben oft Abstürze.",
        "sg.slow_start": "Spielstart dauerte {seconds:.1f}s. Große Mods können die Ladezeit erhöhen.",
    },
    "it": {
        "app_title": "Dottore dei Log SMAPI",
        "btn_open": "Apri Log SMAPI",
        "btn_export": "Esporta Sommario (TXT)",
        "btn_export_html": "Esporta Sommario (HTML)",
        "status_ready": "Pronto. Apri un log SMAPI per analizzare.",
        "status_loaded": "Log caricato: {path}",
        "status_no_analysis": "Nessuna analisi. Apri prima un log.",
        "status_export_ok": "Sommario esportato in {path}",
        "status_export_fail": "Export fallito: {error}",
        "status_export_html_ok": "Report HTML esportato in {path}",
        "status_export_html_fail": "Export HTML fallito: {error}",
        "tab_overview": "Panoramica",
        "tab_mod_health": "Salute Mod",
        "tab_errors": "Errori",
        "tab_warnings": "Avvisi",
        "tab_suggestions": "Suggerimenti",
        "tab_raw": "Log Grezzo",
        "overview_title": "Panoramica Stardew Valley / SMAPI",
        "overview_game_version": "Versione Gioco",
        "overview_smapi_version": "Versione SMAPI",
        "overview_unknown": "Sconosciuto",
        "overview_summary": "Riepilogo",
        "overview_mod_count": "Mod caricate: {count}",
        "overview_content_pack_count": "Content pack caricati: {count}",
        "overview_error_count": "Errori: {count}",
        "overview_warning_count": "Avvisi: {count}",
        "overview_slow_start": "Tempo avvio: {seconds:.1f}s",
        "overview_hint": "Consiglio: risolvi prima gli errori, poi gli avvisi.",
        "mod_health_title": "Salute e Rischi Mod",
        "mod_health_patched_header": "Mod che modificano il codice di gioco (rischio alto):",
        "mod_health_save_header": "Mod che cambiano il salvataggio (NON rimuovere a metà partita):",
        "mod_health_console_header": "Mod con accesso diretto alla console:",
        "mod_health_missing_dep_header": "Mod con dipendenze mancanti:",
        "mod_health_missing_dep_item": "{mod} → manca: {missing}",
        "mod_health_none": "Nessuna mod rischiosa rilevata.",
        "mod_health_updates_header": "Aggiornamenti disponibili:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Errori trovati",
        "errors_none": "Nessun errore rilevato. 🎉",
        "errors_intro": "Questi sono i problemi più importanti segnalati:",
        "warnings_header": "Avvisi",
        "warnings_none": "Nessun avviso trovato.",
        "warnings_intro": "Potrebbero non rompere il gioco subito, ma controllali:",
        "suggestions_header": "Soluzioni suggerite",
        "suggestions_none": "Nessun suggerimento automatico. Controlla tab Errori/Avvisi.",
        "raw_header": "Log SMAPI Completo",
        "warn_rivatuner": "RivaTuner Statistics Server rilevato. Può causare crash con SMAPI.",
        "sg.skipped_mod": "Sistema mod \"{name}\": SMAPI l'ha saltata ({reason}). Controlla manifest.json.",
        "sg.failed_mod": "Sistema mod \"{name}\": Caricamento fallito ({reason}). Controlla le istruzioni.",
        "sg.missing_dep": "Installa dipendenza \"{missing}\" per \"{mod}\".",
        "sg.save_serializer": "\"{mod}\" cambia il salvataggio. Fai backup e non rimuoverla a metà partita.",
        "sg.patched_mods_many": "Molte mod modificano il codice ({count}). Se hai crash, disabilita le mod utility.",
        "sg.rivatuner": "RivaTuner Statistics Server può confliggere con SMAPI. Aggiungi un'eccezione.",
        "sg.updates": "Puoi aggiornare {count} mod. Gli aggiornamenti spesso risolvono crash.",
        "sg.slow_start": "Avvio in {seconds:.1f}s. Molte mod pesanti rallentano il caricamento.",
    },
    "ja": {
        "app_title": "SMAPI ログドクター",
        "btn_open": "SMAPIログを開く",
        "btn_export": "概要をエクスポート (TXT)",
        "btn_export_html": "概要をエクスポート (HTML)",
        "status_ready": "準備完了。SMAPIログを開いて分析してください。",
        "status_loaded": "読み込み完了: {path}",
        "status_no_analysis": "分析結果がありません。先にログを開いてください。",
        "status_export_ok": "概要を {path} に保存しました",
        "status_export_fail": "エクスポート失敗: {error}",
        "status_export_html_ok": "HTMLレポートを {path} に保存しました",
        "status_export_html_fail": "HTMLレポート失敗: {error}",
        "tab_overview": "概要",
        "tab_mod_health": "Modの健全性",
        "tab_errors": "エラー",
        "tab_warnings": "警告",
        "tab_suggestions": "提案",
        "tab_raw": "ログ原文",
        "overview_title": "Stardew Valley / SMAPI 概要",
        "overview_game_version": "ゲームバージョン",
        "overview_smapi_version": "SMAPIバージョン",
        "overview_unknown": "不明",
        "overview_summary": "サマリー",
        "overview_mod_count": "読み込まれたMod: {count}",
        "overview_content_pack_count": "読み込まれたコンテンツパック: {count}",
        "overview_error_count": "エラー数: {count}",
        "overview_warning_count": "警告数: {count}",
        "overview_slow_start": "起動時間: {seconds:.1f}秒",
        "overview_hint": "ヒント: まずエラーを修正し、次に警告、最後にその他の問題を確認してください。",
        "mod_health_title": "Modの健全性とリスク",
        "mod_health_patched_header": "ゲームコードを改変するMod (高リスク):",
        "mod_health_save_header": "セーブ形式を変更するMod (プレイ途中で削除しないでください):",
        "mod_health_console_header": "コンソールに直接アクセスするMod:",
        "mod_health_missing_dep_header": "前提Modが不足しているMod:",
        "mod_health_missing_dep_item": "{mod} → 不足: {missing}",
        "mod_health_none": "リスクの高いModは検出されませんでした。",
        "mod_health_updates_header": "アップデート可能なMod:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "検出されたエラー",
        "errors_none": "SMAPI/ゲームのエラーは検出されませんでした。🎉",
        "errors_intro": "以下はSMAPIまたはゲームによって報告された重要な問題です:",
        "warnings_header": "警告",
        "warnings_none": "警告は見つかりませんでした。",
        "warnings_intro": "直ちにゲームが停止するわけではありませんが、確認する価値があります:",
        "suggestions_header": "推奨される修正",
        "suggestions_none": "自動的な提案はありません。問題が続く場合はエラー/警告タブを確認してください。",
        "raw_header": "SMAPI ログ全文",
        "warn_rivatuner": "RivaTuner Statistics Serverが検出されました。SMAPIと競合しクラッシュする可能性があります。",
        "sg.skipped_mod": "Mod「{name}」を修正: SMAPIがスキップしました ({reason})。フォルダ内のmanifest.jsonを確認してください。",
        "sg.failed_mod": "Mod「{name}」を修正: 読み込みに失敗しました ({reason})。配布ページのインストール手順を確認してください。",
        "sg.missing_dep": "「{mod}」に必要な前提Mod「{missing}」をインストールするか、不要であれば無効化してください。",
        "sg.save_serializer": "「{mod}」はセーブ形式を変更します。バックアップを取り、プレイ途中で削除しないようにしてください。",
        "sg.patched_mods_many": "ゲームコードを改変するModが多数あります ({count})。動作が不安定な場合、ツール系Modを無効化して確認してください。",
        "sg.rivatuner": "RivaTuner Statistics ServerはSMAPIと競合する可能性があります。Stardew Valleyを例外に追加してください。",
        "sg.updates": "{count}個のModを更新できます。主要なModを最新に保つことでクラッシュを防げます。",
        "sg.slow_start": "ゲームの起動に約{seconds:.1f}秒かかりました。大規模なModは読み込み時間を増加させます。",
    },
    "ko": {
        "app_title": "SMAPI 로그 닥터",
        "btn_open": "SMAPI 로그 열기",
        "btn_export": "요약 내보내기 (TXT)",
        "btn_export_html": "요약 내보내기 (HTML)",
        "status_ready": "준비됨. 분석할 SMAPI 로그를 열어주세요.",
        "status_loaded": "로그 로드됨: {path}",
        "status_no_analysis": "분석 결과 없음. 먼저 로그를 여세요.",
        "status_export_ok": "요약이 {path}에 저장됨",
        "status_export_fail": "내보내기 실패: {error}",
        "status_export_html_ok": "HTML 보고서가 {path}에 저장됨",
        "status_export_html_fail": "HTML 내보내기 실패: {error}",
        "tab_overview": "개요",
        "tab_mod_health": "모드 상태",
        "tab_errors": "오류",
        "tab_warnings": "경고",
        "tab_suggestions": "제안",
        "tab_raw": "원본 로그",
        "overview_title": "스타듀 밸리 / SMAPI 개요",
        "overview_game_version": "게임 버전",
        "overview_smapi_version": "SMAPI 버전",
        "overview_unknown": "알 수 없음",
        "overview_summary": "요약",
        "overview_mod_count": "로드된 모드: {count}",
        "overview_content_pack_count": "로드된 콘텐츠 팩: {count}",
        "overview_error_count": "오류: {count}",
        "overview_warning_count": "경고: {count}",
        "overview_slow_start": "시동 시간: {seconds:.1f}초",
        "overview_hint": "팁: 오류를 먼저 수정하고, 경고를 확인한 뒤 최적화 문제를 해결하세요.",
        "mod_health_title": "모드 상태 및 위험",
        "mod_health_patched_header": "게임 코드를 수정하는 모드 (높은 위험):",
        "mod_health_save_header": "저장 방식을 변경하는 모드 (플레이 도중 삭제 금지):",
        "mod_health_console_header": "콘솔에 직접 접근하는 모드:",
        "mod_health_missing_dep_header": "선행 모드가 누락된 모드:",
        "mod_health_missing_dep_item": "{mod} → 누락: {missing}",
        "mod_health_none": "위험한 모드가 감지되지 않았습니다.",
        "mod_health_updates_header": "업데이트 가능한 모드:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "발견된 오류",
        "errors_none": "SMAPI/게임 오류가 감지되지 않았습니다. 🎉",
        "errors_intro": "SMAPI 또는 게임에서 보고한 주요 문제입니다:",
        "warnings_header": "경고",
        "warnings_none": "경고가 발견되지 않았습니다.",
        "warnings_intro": "게임이 즉시 멈추지는 않겠지만 확인이 필요합니다:",
        "suggestions_header": "추천 해결법",
        "suggestions_none": "자동 제안 없음. 오류/경고 탭을 확인하세요.",
        "raw_header": "전체 SMAPI 로그",
        "warn_rivatuner": "RivaTuner Statistics Server 감지됨. SMAPI와 충돌할 수 있습니다.",
        "sg.skipped_mod": "\"{name}\" 모드 수정: SMAPI가 건너뛰었습니다 ({reason}). 폴더와 manifest.json을 확인하세요.",
        "sg.failed_mod": "\"{name}\" 모드 수정: 로드 실패 ({reason}). 설치 방법을 확인하세요.",
        "sg.missing_dep": "\"{mod}\"에 필요한 선행 모드 \"{missing}\"을(를) 설치하세요.",
        "sg.save_serializer": "\"{mod}\"은(는) 저장 방식을 변경합니다. 백업을 하고 도중에 삭제하지 마세요.",
        "sg.patched_mods_many": "게임 코드를 수정하는 모드가 많습니다 ({count}). 충돌 시 유틸리티 모드를 확인하세요.",
        "sg.rivatuner": "RivaTuner Statistics Server는 SMAPI와 충돌할 수 있습니다. 예외에 추가하세요.",
        "sg.updates": "{count}개의 모드를 업데이트할 수 있습니다. 최신 상태 유지는 충돌을 방지합니다.",
        "sg.slow_start": "게임 시작에 {seconds:.1f}초가 걸렸습니다. 대형 모드는 로딩 시간을 늘릴 수 있습니다.",
    },
    "pl": {
        "app_title": "Doktor Logów SMAPI",
        "btn_open": "Otwórz log SMAPI",
        "btn_export": "Eksportuj podsumowanie (TXT)",
        "btn_export_html": "Eksportuj podsumowanie (HTML)",
        "status_ready": "Gotowy. Otwórz log SMAPI do analizy.",
        "status_loaded": "Załadowano log: {path}",
        "status_no_analysis": "Brak analizy. Najpierw otwórz log.",
        "status_export_ok": "Eksport zakończony: {path}",
        "status_export_fail": "Błąd eksportu: {error}",
        "status_export_html_ok": "Raport HTML wyeksportowany: {path}",
        "status_export_html_fail": "Błąd eksportu HTML: {error}",
        "tab_overview": "Przegląd",
        "tab_mod_health": "Stan Modów",
        "tab_errors": "Błędy",
        "tab_warnings": "Ostrzeżenia",
        "tab_suggestions": "Sugestie",
        "tab_raw": "Surowy Log",
        "overview_title": "Przegląd Stardew Valley / SMAPI",
        "overview_game_version": "Wersja gry",
        "overview_smapi_version": "Wersja SMAPI",
        "overview_unknown": "Nieznana",
        "overview_summary": "Podsumowanie",
        "overview_mod_count": "Załadowane mody: {count}",
        "overview_content_pack_count": "Załadowane paczki: {count}",
        "overview_error_count": "Błędy: {count}",
        "overview_warning_count": "Ostrzeżenia: {count}",
        "overview_slow_start": "Czas uruchomienia: {seconds:.1f}s",
        "overview_hint": "Wskazówka: najpierw napraw błędy, potem ostrzeżenia.",
        "mod_health_title": "Stan i Ryzyko Modów",
        "mod_health_patched_header": "Mody modyfikujące kod gry (wyższe ryzyko):",
        "mod_health_save_header": "Mody zmieniające zapis (NIE usuwać w trakcie gry):",
        "mod_health_console_header": "Mody z dostępem do konsoli:",
        "mod_health_missing_dep_header": "Mody z brakującymi zależnościami:",
        "mod_health_missing_dep_item": "{mod} → brakuje: {missing}",
        "mod_health_none": "Nie wykryto ryzykownych modów.",
        "mod_health_updates_header": "Dostępne aktualizacje:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Znalezione błędy",
        "errors_none": "Brak błędów SMAPI/gry. 🎉",
        "errors_intro": "To najważniejsze problemy zgłoszone przez SMAPI:",
        "warnings_header": "Ostrzeżenia",
        "warnings_none": "Brak ostrzeżeń.",
        "warnings_intro": "Mogą nie psuć gry od razu, ale warto sprawdzić:",
        "suggestions_header": "Sugerowane poprawki",
        "suggestions_none": "Brak automatycznych sugestii. Sprawdź zakładki Błędy/Ostrzeżenia.",
        "raw_header": "Pełny Log SMAPI",
        "warn_rivatuner": "Wykryto RivaTuner Statistics Server. Może powodować błędy z SMAPI.",
        "sg.skipped_mod": "Napraw mod \"{name}\": SMAPI go pominęło ({reason}). Sprawdź manifest.json.",
        "sg.failed_mod": "Napraw mod \"{name}\": Nie udało się załadować ({reason}). Sprawdź instrukcję.",
        "sg.missing_dep": "Zainstaluj wymaganą zależność \"{missing}\" dla \"{mod}\".",
        "sg.save_serializer": "\"{mod}\" zmienia sposób zapisu. Zrób kopię zapasową i nie usuwaj go w trakcie gry.",
        "sg.patched_mods_many": "Wiele modów zmienia kod gry ({count}). W razie problemów wyłącz mody narzędziowe.",
        "sg.rivatuner": "RivaTuner Statistics Server może kolidować z SMAPI. Dodaj wyjątek.",
        "sg.updates": "Możesz zaktualizować {count} modów. Aktualizacje często naprawiają błędy.",
        "sg.slow_start": "Uruchomienie zajęło {seconds:.1f}s. Duże mody mogą wydłużyć ładowanie.",
    },
    "pt-br": {
        "app_title": "Doutor de Logs do SMAPI",
        "btn_open": "Abrir log do SMAPI",
        "btn_export": "Exportar resumo (TXT)",
        "btn_export_html": "Exportar resumo (HTML)",
        "status_ready": "Pronto. Abra um log do SMAPI para analisar.",
        "status_loaded": "Log carregado: {path}",
        "status_no_analysis": "Ainda não há análise. Abra um log primeiro.",
        "status_export_ok": "Resumo exportado para {path}",
        "status_export_fail": "Falha ao exportar resumo: {error}",
        "status_export_html_ok": "Relatório HTML exportado para {path}",
        "status_export_html_fail": "Falha ao exportar relatório HTML: {error}",
        "tab_overview": "Visão geral",
        "tab_mod_health": "Saúde dos mods",
        "tab_errors": "Erros",
        "tab_warnings": "Avisos",
        "tab_suggestions": "Sugestões",
        "tab_raw": "Log bruto",
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
        "mod_health_title": "Saúde e risco dos mods",
        "mod_health_patched_header": "Mods que alteram o código do jogo (risco maior):",
        "mod_health_save_header": "Mods que mudam o serializador de salvamento (não remova no meio de um save):",
        "mod_health_console_header": "Mods com acesso direto ao console:",
        "mod_health_missing_dep_header": "Mods com dependências ausentes:",
        "mod_health_missing_dep_item": "{mod} → faltando: {missing}",
        "mod_health_none": "Nenhum mod claramente arriscado foi detectado neste log.",
        "mod_health_updates_header": "Mods com atualizações disponíveis:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Erros encontrados neste log",
        "errors_none": "Nenhum erro do SMAPI/jogo foi encontrado. 🎉",
        "errors_intro": "Estes são os problemas mais importantes relatados pelo SMAPI ou pelo jogo:",
        "warnings_header": "Avisos",
        "warnings_none": "Nenhum aviso encontrado.",
        "warnings_intro": "Eles podem não quebrar o jogo na hora, mas valem a sua atenção:",
        "suggestions_header": "Sugestões de correção",
        "suggestions_none": "Nenhuma sugestão automática por enquanto. Se o jogo ainda estiver estranho, confira as abas de Erros e Avisos.",
        "raw_header": "Log completo do SMAPI",
        "warn_rivatuner": "RivaTuner Statistics Server detectado. Ele pode causar crashes com o SMAPI; adicione uma exceção ou desative-o.",
        "sg.skipped_mod": "Corrija o mod {name}: o SMAPI pulou ele ({reason}). Abra a pasta do mod e verifique se o manifest.json é válido e se a versão é compatível com o seu jogo/SMAPI.",
        "sg.failed_mod": "Corrija o mod {name}: o SMAPI não conseguiu carregá-lo ({reason}). Veja as instruções de instalação na página do mod e reinstale se necessário.",
        "sg.missing_dep": "Instale a dependência obrigatória {missing} para o mod {mod}, ou desative o mod se não for usá-lo.",
        "sg.save_serializer": "{mod} altera a forma como o jogo salva. Faça backup dos saves e não remova esse mod no meio de um save.",
        "sg.patched_mods_many": "Você tem muitos mods alterando o código do jogo ({count}). Se aparecerem crashes estranhos, tente desativar utilidades/FX uma por vez.",
        "sg.rivatuner": "RivaTuner Statistics Server pode entrar em conflito com o SMAPI. Adicione uma exceção para Stardew Valley ou feche o programa enquanto joga.",
        "sg.updates": "{count} mod(s) podem ser atualizados. Manter frameworks e mods de base atualizados costuma resolver crashes e problemas invisíveis.",
        "sg.slow_start": "A inicialização do jogo levou cerca de {seconds:.1f}s. Muitos content packs e mods pesados aumentam o tempo de carregamento; se incomodar, considere enxugar um pouco a lista.",
    },
    "tr": {
        "app_title": "SMAPI Günlük Doktoru",
        "btn_open": "SMAPI Günlüğü Aç",
        "btn_export": "Özeti Dışa Aktar (TXT)",
        "btn_export_html": "Özeti Dışa Aktar (HTML)",
        "status_ready": "Hazır. Analiz için bir SMAPI günlüğü açın.",
        "status_loaded": "Günlük yüklendi: {path}",
        "status_no_analysis": "Henüz analiz yok. Önce bir günlük açın.",
        "status_export_ok": "Özet dışa aktarıldı: {path}",
        "status_export_fail": "Dışa aktarma başarısız: {error}",
        "status_export_html_ok": "HTML raporu dışa aktarıldı: {path}",
        "status_export_html_fail": "HTML raporu başarısız: {error}",
        "tab_overview": "Genel Bakış",
        "tab_mod_health": "Mod Sağlığı",
        "tab_errors": "Hatalar",
        "tab_warnings": "Uyarılar",
        "tab_suggestions": "Öneriler",
        "tab_raw": "Ham Günlük",
        "overview_title": "Stardew Valley / SMAPI Genel Bakış",
        "overview_game_version": "Oyun Sürümü",
        "overview_smapi_version": "SMAPI Sürümü",
        "overview_unknown": "Bilinmiyor",
        "overview_summary": "Özet",
        "overview_mod_count": "Yüklü modlar: {count}",
        "overview_content_pack_count": "Yüklü içerik paketleri: {count}",
        "overview_error_count": "Hatalar: {count}",
        "overview_warning_count": "Uyarılar: {count}",
        "overview_slow_start": "Başlangıç süresi: {seconds:.1f}s",
        "overview_hint": "İpucu: Önce hataları, sonra uyarıları düzeltin.",
        "mod_health_title": "Mod Sağlığı ve Riskler",
        "mod_health_patched_header": "Oyun kodunu yamalayan modlar (yüksek risk):",
        "mod_health_save_header": "Kayıt yöntemini değiştiren modlar (oyun ortasında kaldırmayın):",
        "mod_health_console_header": "Konsola doğrudan erişen modlar:",
        "mod_health_missing_dep_header": "Eksik bağımlılığı olan modlar:",
        "mod_health_missing_dep_item": "{mod} → eksik: {missing}",
        "mod_health_none": "Riskli mod tespit edilmedi.",
        "mod_health_updates_header": "Güncellemesi olan modlar:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Bulunan Hatalar",
        "errors_none": "SMAPI/oyun hatası tespit edilmedi. 🎉",
        "errors_intro": "Bunlar SMAPI tarafından bildirilen en önemli sorunlardır:",
        "warnings_header": "Uyarılar",
        "warnings_none": "Uyarı bulunamadı.",
        "warnings_intro": "Oyunu hemen bozmayabilirler ama kontrol etmeye değer:",
        "suggestions_header": "Önerilen Çözümler",
        "suggestions_none": "Otomatik öneri yok. Hatalar/Uyarılar sekmelerine bakın.",
        "raw_header": "Tam SMAPI Günlüğü",
        "warn_rivatuner": "RivaTuner Statistics Server tespit edildi. SMAPI ile çökmelere neden olabilir.",
        "sg.skipped_mod": "\"{name}\" modunu düzelt: SMAPI atladı ({reason}). manifest.json dosyasını kontrol et.",
        "sg.failed_mod": "\"{name}\" modunu düzelt: Yüklenemedi ({reason}). Kurulum talimatlarını kontrol et.",
        "sg.missing_dep": "\"{mod}\" için gerekli \"{missing}\" bağımlılığını yükle.",
        "sg.save_serializer": "\"{mod}\" kayıt yöntemini değiştiriyor. Yedek al ve oyun ortasında silme.",
        "sg.patched_mods_many": "Çok sayıda mod oyun kodunu değiştiriyor ({count}). Sorun yaşarsan yardımcı modları kapat.",
        "sg.rivatuner": "RivaTuner Statistics Server SMAPI ile çakışabilir. Bir istisna ekleyin.",
        "sg.updates": "{count} modu güncelleyebilirsin. Güncellemeler genellikle çökmeleri düzeltir.",
        "sg.slow_start": "Başlangıç {seconds:.1f}s sürdü. Büyük modlar yükleme süresini uzatabilir.",
    },
    "ua": {
        "app_title": "Лікар логів SMAPI",
        "btn_open": "Відкрити лог SMAPI",
        "btn_export": "Експортувати звіт (TXT)",
        "btn_export_html": "Експортувати звіт (HTML)",
        "status_ready": "Готово. Відкрийте лог SMAPI для аналізу.",
        "status_loaded": "Лог завантажено: {path}",
        "status_no_analysis": "Аналізу ще немає. Спочатку відкрийте лог.",
        "status_export_ok": "Звіт збережено в {path}",
        "status_export_fail": "Не вдалося експортувати: {error}",
        "status_export_html_ok": "HTML-звіт збережено в {path}",
        "status_export_html_fail": "Не вдалося експортувати HTML: {error}",
        "tab_overview": "Огляд",
        "tab_mod_health": "Здоров'я модів",
        "tab_errors": "Помилки",
        "tab_warnings": "Попередження",
        "tab_suggestions": "Пропозиції",
        "tab_raw": "Сирий лог",
        "overview_title": "Огляд Stardew Valley / SMAPI",
        "overview_game_version": "Версія гри",
        "overview_smapi_version": "Версія SMAPI",
        "overview_unknown": "Невідомо",
        "overview_summary": "Підсумок",
        "overview_mod_count": "Завантажено модів: {count}",
        "overview_content_pack_count": "Завантажено контент-паків: {count}",
        "overview_error_count": "Помилок: {count}",
        "overview_warning_count": "Попереджень: {count}",
        "overview_slow_start": "Час запуску: {seconds:.1f} с",
        "overview_hint": "Порада: спочатку виправляйте помилки, потім попередження.",
        "mod_health_title": "Стан та ризик модів",
        "mod_health_patched_header": "Моди, що змінюють код гри (підвищений ризик):",
        "mod_health_save_header": "Моди, що змінюють збереження (не видаляти під час проходження):",
        "mod_health_console_header": "Моди з доступом до консолі:",
        "mod_health_missing_dep_header": "Моди з відсутніми залежностями:",
        "mod_health_missing_dep_item": "{mod} → відсутнє: {missing}",
        "mod_health_none": "Ризикованих модів не виявлено.",
        "mod_health_updates_header": "Доступні оновлення:",
        "mod_health_update_item": "{name} {current} → {latest}",
        "errors_header": "Знайдені помилки",
        "errors_none": "Помилок SMAPI / гри не знайдено. 🎉",
        "errors_intro": "Це найважливіші проблеми, про які повідомляє SMAPI:",
        "warnings_header": "Попередження",
        "warnings_none": "Попереджень не знайдено.",
        "warnings_intro": "Вони не обов'язково зламають гру, але варто перевірити:",
        "suggestions_header": "Рекомендовані дії",
        "suggestions_none": "Автоматичних рекомендацій немає. Перевірте вкладки Помилки/Попередження.",
        "raw_header": "Повний лог SMAPI",
        "warn_rivatuner": "Виявлено RivaTuner Statistics Server. Може викликати вильоти SMAPI.",
        "sg.skipped_mod": "Виправте мод \"{name}\": SMAPI пропустив його ({reason}). Перевірте manifest.json.",
        "sg.failed_mod": "Виправте мод \"{name}\": Не вдалося завантажити ({reason}). Перевірте інструкцію.",
        "sg.missing_dep": "Встановіть залежність \"{missing}\" для \"{mod}\".",
        "sg.save_serializer": "\"{mod}\" змінює спосіб збереження. Зробіть бекап і не видаляйте цей мод посеред гри.",
        "sg.patched_mods_many": "Багато модів змінюють код гри ({count}). Якщо гра вилітає, спробуйте вимкнути допоміжні моди.",
        "sg.rivatuner": "RivaTuner Statistics Server може конфліктувати з SMAPI. Додайте виняток.",
        "sg.updates": "Можна оновити {count} модів. Оновлення часто виправляють помилки.",
        "sg.slow_start": "Запуск гри зайняв {seconds:.1f} с. Великі моди збільшують час завантаження.",
    },
    "ru": {
        # window
        "app_title": "Доктор логов SMAPI",
        "btn_open": "Открыть лог SMAPI",
        "btn_export": "Экспортировать сводку (TXT)",
        "btn_export_html": "Экспортировать сводку (HTML)",
        "status_ready": "Готово. Сначала откройте лог SMAPI для анализа.",
        "status_loaded": "Лог загружен: {path}",
        "status_no_analysis": "Анализа ещё нет. Сначала откройте лог.",
        "status_export_ok": "Сводка сохранена в {path}",
        "status_export_fail": "Не удалось экспортировать сводку: {error}",
        "status_export_html_ok": "HTML-отчёт сохранён в {path}",
        "status_export_html_fail": "Не удалось экспортировать HTML-отчёт: {error}",

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
        "errors_none": "Ошибок SMAPI / игры не найдено. 🎉",
        "errors_intro": "Это наиболее важные проблемы, о которых сообщает SMAPI или сама игра:",

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
        "btn_export": "Exportar resumo (TXT)",
        "btn_export_html": "Exportar resumo (HTML)",
        "status_ready": "Pronto. Abra um log do SMAPI para analisar.",
        "status_loaded": "Log carregado: {path}",
        "status_no_analysis": "Ainda não há análise. Abra um log primeiro.",
        "status_export_ok": "Resumo exportado para {path}",
        "status_export_fail": "Falha ao exportar resumo: {error}",
        "status_export_html_ok": "Relatório HTML exportado para {path}",
        "status_export_html_fail": "Falha ao exportar relatório HTML: {error}",

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
        "errors_none": "Nenhum erro do SMAPI/jogo foi encontrado. 🎉",
        "errors_intro": "Estes são os problemas mais importantes relatados pelo SMAPI ou pelo jogo:",

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

        # Generic ERROR / WARN lines, including game/mod errors, not just SMAPI
        if "ERROR" in line and "Skipped mods" not in line:
            msg = line
            # Strip [HH:MM:SS ...] prefix
            msg = re.sub(r"^\[\d{2}:\d{2}:\d{2} [^\]]*\]\s*", "", msg)
            # Strip HH:MM:SS prefix without brackets
            msg = re.sub(r"^\d{2}:\d{2}:\d{2}\s+", "", msg)
            msg = msg.strip()
            if msg:
                analysis.errors.append(msg)

        if "WARN" in line and "Changed save serializer" not in line:
            msg = line
            msg = re.sub(r"^\[\d{2}:\d{2}:\d{2} [^\]]*\]\s*", "", msg)
            msg = re.sub(r"^\d{2}:\d{2}:\d{2}\s+", "", msg)
            msg = msg.strip()
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
# Config helpers (remember last dir)
# =========================

CONFIG_PATH = os.path.join(os.path.expanduser("~"), "smapi_log_doctor_config.json")


@dataclass
class AppConfig:
    last_log_dir: Optional[str] = None


def load_config() -> AppConfig:
    try:
        if os.path.exists(CONFIG_PATH):
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
            return AppConfig(**data)
    except Exception:
        pass
    return AppConfig()


def save_config(cfg: AppConfig) -> None:
    try:
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(cfg.__dict__, f, ensure_ascii=False, indent=2)
    except Exception:
        pass


def guess_smapi_log_dir() -> Optional[str]:
    # try %APPDATA%\StardewValley\ErrorLogs etc
    appdata = os.getenv("APPDATA")
    candidates: List[str] = []
    if appdata:
        candidates.append(os.path.join(appdata, "StardewValley", "ErrorLogs"))
        candidates.append(os.path.join(appdata, "StardewValley"))
    for c in candidates:
        if os.path.isdir(c):
            return c
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

        self.config = load_config()

        self.root.title(TEXT[self.lang]["app_title"])
        self.root.geometry("1000x720")

        self._build_ui()

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

        self.btn_export = ttk.Button(toolbar, text=self._t("btn_export"), command=self.export_summary_txt)
        self.btn_export.pack(side="left", padx=(4, 0))

        self.btn_export_html = ttk.Button(toolbar, text=self._t("btn_export_html"), command=self.export_summary_html)
        self.btn_export_html.pack(side="left", padx=(4, 0))

        # Language dropdown
        lang_frame = ttk.Frame(toolbar)
        lang_frame.pack(side="right")

        ttk.Label(lang_frame, text="Language:").pack(side="left", padx=(0, 4))

        self.lang_var = tk.StringVar(value=self.lang)
        self.lang_combo = ttk.Combobox(
            lang_frame,
            textvariable=self.lang_var,
            state="readonly",
            width=8,
            values=["en", "es", "fr", "de", "it", "ja", "ko", "pl", "pt-br", "tr", "ua", "zh", "ru", "pt"],
        )
        self.lang_combo.pack(side="left")
        self.lang_combo.bind("<<ComboboxSelected>>", self._on_lang_changed)

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

    # ---------- Language toggle ----------

    def _on_lang_changed(self, event=None) -> None:
        new_lang = self.lang_var.get()
        self.set_language(new_lang)

    def set_language(self, lang: str) -> None:
        if lang not in TEXT:
            return
        if lang == self.lang:
            return
        self.lang = lang
        self.root.title(TEXT[self.lang]["app_title"])
        # Update button labels & tab titles
        self.btn_open.config(text=self._t("btn_open"))
        self.btn_export.config(text=self._t("btn_export"))
        self.btn_export_html.config(text=self._t("btn_export_html"))

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

    # ---------- File handling ----------

    def open_log(self) -> None:
        initialdir = (
            self.config.last_log_dir
            or guess_smapi_log_dir()
            or os.path.expanduser("~")
        )

        path = filedialog.askopenfilename(
            title="Select SMAPI log",
            initialdir=initialdir,
            filetypes=[
                ("Text files", "*.txt"),
                ("All files", "*.*"),
            ],
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
        self.config.last_log_dir = os.path.dirname(path)
        save_config(self.config)

        self.render_all()
        self.status_var.set(self._t("status_loaded", path=path))

    def export_summary_txt(self) -> None:
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

    def export_summary_html(self) -> None:
        if not self.analysis:
            messagebox.showinfo("Info", self._t("status_no_analysis"))
            return
        path = filedialog.asksaveasfilename(
            title="Export HTML report",
            defaultextension=".html",
            filetypes=[("HTML files", "*.html;*.htm")],
        )
        if not path:
            return

        try:
            html_text = self._build_html_summary()
            with open(path, "w", encoding="utf-8") as f:
                f.write(html_text)
            self.status_var.set(self._t("status_export_html_ok", path=path))
        except Exception as e:
            self.status_var.set(self._t("status_export_html_fail", error=e))

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
            lower = s.lower()
            if ("save" in lower or "存档" in s or "сейв" in lower or "salva" in lower):
                tags.append("error")
            elif ("update" in lower or "更新" in s or "обнов" in lower or "atualiz" in lower):
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

    # ---------- Export summary (plain text & HTML) ----------

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

    def _build_html_summary(self) -> str:
        if not self.analysis:
            return ""
        a = self.analysis
        t = self._t

        def esc(s: str) -> str:
            return html.escape(str(s), quote=True)

        parts: List[str] = []
        parts.append("<!DOCTYPE html>")
        parts.append("<html><head><meta charset='utf-8'>")
        parts.append(f"<title>{esc(t('app_title'))}</title>")
        parts.append(
            "<style>"
            "body{font-family:Segoe UI,system-ui,-apple-system,sans-serif;background:#121212;color:#eee;margin:0;padding:16px;}"
            "h1,h2,h3{color:#ffd369;}"
            "section{margin-bottom:24px;padding:16px;border-radius:8px;background:#1e1e1e;box-shadow:0 0 8px rgba(0,0,0,0.6);}"
            "ul{margin:8px 0 0 20px;padding:0;}"
            ".error{color:#ff6b6b;}"
            ".warn{color:#ffb347;}"
            ".info{color:#4da3ff;}"
            ".muted{color:#999;}"
            "code{background:#222;border-radius:4px;padding:2px 4px;}"
            "</style>"
        )
        parts.append("</head><body>")

        # Overview
        parts.append("<section>")
        parts.append(f"<h1>{esc(t('overview_title'))}</h1>")
        parts.append("<p>")
        parts.append(f"{esc(t('overview_game_version'))}: <strong>{esc(a.game_version or t('overview_unknown'))}</strong><br>")
        parts.append(f"{esc(t('overview_smapi_version'))}: <strong>{esc(a.smapi_version or t('overview_unknown'))}</strong><br>")
        parts.append(f"{esc(t('overview_mod_count', count=a.mod_count))}<br>")
        parts.append(f"{esc(t('overview_content_pack_count', count=a.content_pack_count))}<br>")
        if a.slow_start_seconds is not None:
            parts.append(f"{esc(t('overview_slow_start', seconds=a.slow_start_seconds))}<br>")
        parts.append("</p>")
        parts.append(f"<p class='muted'>{esc(t('overview_hint'))}</p>")
        parts.append("</section>")

        # Errors
        parts.append("<section>")
        parts.append(f"<h2>{esc(t('errors_header'))}</h2>")
        if not a.errors and not a.skipped_mods and not a.failed_mods:
            parts.append(f"<p class='info'>{esc(t('errors_none'))}</p>")
        else:
            parts.append("<ul>")
            for sm in a.skipped_mods:
                parts.append(
                    f"<li class='error'>[Skipped] {esc(sm.name)} — {esc(sm.reason)}</li>"
                )
            for fm in a.failed_mods:
                parts.append(
                    f"<li class='error'>[Failed] {esc(fm.name)} — {esc(fm.reason)}</li>"
                )
            for e in a.errors:
                parts.append(f"<li class='error'>{esc(e)}</li>")
            parts.append("</ul>")
        parts.append("</section>")

        # Warnings
        parts.append("<section>")
        parts.append(f"<h2>{esc(t('warnings_header'))}</h2>")
        if not a.warnings and not a.external_conflicts:
            parts.append(f"<p class='info'>{esc(t('warnings_none'))}</p>")
        else:
            parts.append("<ul>")
            for w in a.warnings:
                parts.append(f"<li class='warn'>{esc(w)}</li>")
            for x in a.external_conflicts:
                if "RivaTuner" in x:
                    parts.append(f"<li class='warn'>{esc(TEXT[self.lang]['warn_rivatuner'])}</li>")
            parts.append("</ul>")
        parts.append("</section>")

        # Mod health
        parts.append("<section>")
        parts.append(f"<h2>{esc(t('mod_health_title'))}</h2>")
        any_mod_health = False
        if a.patched_mods:
            any_mod_health = True
            parts.append(f"<h3>{esc(t('mod_health_patched_header'))}</h3><ul>")
            for m in a.patched_mods:
                parts.append(f"<li class='warn'>{esc(m)}</li>")
            parts.append("</ul>")
        if a.save_serializer_mods:
            any_mod_health = True
            parts.append(f"<h3>{esc(t('mod_health_save_header'))}</h3><ul>")
            for m in a.save_serializer_mods:
                parts.append(f"<li class='error'>{esc(m)}</li>")
            parts.append("</ul>")
        if a.direct_console_mods:
            any_mod_health = True
            parts.append(f"<h3>{esc(t('mod_health_console_header'))}</h3><ul>")
            for m in a.direct_console_mods:
                parts.append(f"<li class='muted'>{esc(m)}</li>")
            parts.append("</ul>")
        if a.missing_dependencies:
            any_mod_health = True
            parts.append(f"<h3>{esc(t('mod_health_missing_dep_header'))}</h3><ul>")
            for dep in a.missing_dependencies:
                parts.append(
                    "<li class='error'>"
                    + esc(
                        t(
                            "mod_health_missing_dep_item",
                            mod=dep.mod_name,
                            missing=dep.missing,
                        )
                    )
                    + "</li>"
                )
            parts.append("</ul>")
        if a.update_infos:
            any_mod_health = True
            parts.append(f"<h3>{esc(t('mod_health_updates_header'))}</h3><ul>")
            for u in a.update_infos:
                parts.append(
                    "<li class='info'>"
                    + esc(
                        t(
                            "mod_health_update_item",
                            name=u.name,
                            current=u.current,
                            latest=u.latest,
                        )
                    )
                    + "</li>"
                )
            parts.append("</ul>")
        if not any_mod_health:
            parts.append(f"<p class='muted'>{esc(t('mod_health_none'))}</p>")
        parts.append("</section>")

        # Suggestions
        parts.append("<section>")
        parts.append(f"<h2>{esc(t('suggestions_header'))}</h2>")
        suggestions = build_suggestions(a, self.lang)
        if not suggestions:
            parts.append(f"<p class='info'>{esc(t('suggestions_none'))}</p>")
        else:
            parts.append("<ul>")
            for s in suggestions:
                lower = s.lower()
                cls = ""
                if ("save" in lower or "存档" in s or "сейв" in lower or "salva" in lower):
                    cls = "error"
                elif ("update" in lower or "更新" in s or "обнов" in lower or "atualiz" in lower):
                    cls = "info"
                elif "RivaTuner" in s:
                    cls = "warn"
                parts.append(f"<li class='{cls}'>{esc(s)}</li>")
            parts.append("</ul>")
        parts.append("</section>")

        # Raw log
        parts.append("<section>")
        parts.append(f"<h2>{esc(t('raw_header'))}</h2>")
        parts.append("<pre>")
        parts.append(esc(a.raw_log))
        parts.append("</pre>")
        parts.append("</section>")

        parts.append("</body></html>")
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