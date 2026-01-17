import json
import zipfile
import io
import os

# The source data and the translations for Stardew Valley modding
translations = {
    "de": {
        "config.section.settings": "Einstellungen",
        "config.enabled.name": "Aktiviert",
        "config.enabled.tooltip": "Schaltet die statische Angelleiste ein oder aus.",
        "config.pos_x.name": "Position X (%)",
        "config.pos_x.tooltip": "Horizontale Position (0 = Links, 1 = Rechts).",
        "config.pos_y.name": "Position Y (%)",
        "config.pos_y.tooltip": "Vertikale Position (0 = Oben, 1 = Unten)."
    },
    "es": {
        "config.section.settings": "Ajustes",
        "config.enabled.name": "Habilitado",
        "config.enabled.tooltip": "Activa o desactiva la barra de pesca estática.",
        "config.pos_x.name": "Posición X (%)",
        "config.pos_x.tooltip": "Posición horizontal (0 = Izquierda, 1 = Derecha).",
        "config.pos_y.name": "Posición Y (%)",
        "config.pos_y.tooltip": "Posición vertical (0 = Arriba, 1 = Abajo)."
    },
    "fr": {
        "config.section.settings": "Paramètres",
        "config.enabled.name": "Activé",
        "config.enabled.tooltip": "Bascule la barre de pêche statique.",
        "config.pos_x.name": "Position X (%)",
        "config.pos_x.tooltip": "Position horizontale (0 = Gauche, 1 = Droite).",
        "config.pos_y.name": "Position Y (%)",
        "config.pos_y.tooltip": "Position verticale (0 = Haut, 1 = Bas)."
    },
    "it": {
        "config.section.settings": "Impostazioni",
        "config.enabled.name": "Abilitato",
        "config.enabled.tooltip": "Attiva/disattiva la barra di pesca statica.",
        "config.pos_x.name": "Posizione X (%)",
        "config.pos_x.tooltip": "Posizione orizzontale (0 = Sinistra, 1 = Destra).",
        "config.pos_y.name": "Posizione Y (%)",
        "config.pos_y.tooltip": "Posizione verticale (0 = Alto, 1 = Basso)."
    },
    "ja": {
        "config.section.settings": "設定",
        "config.enabled.name": "有効",
        "config.enabled.tooltip": "静止した釣りバー機能を切り替えます。",
        "config.pos_x.name": "位置 X (%)",
        "config.pos_x.tooltip": "水平方向の位置 (0 = 左, 1 = 右)。",
        "config.pos_y.name": "位置 Y (%)",
        "config.pos_y.tooltip": "垂直方向の位置 (0 = 上, 1 = 下)。"
    },
    "ko": {
        "config.section.settings": "설정",
        "config.enabled.name": "활성화됨",
        "config.enabled.tooltip": "고정된 낚시바 기능을 켭니다.",
        "config.pos_x.name": "위치 X (%)",
        "config.pos_x.tooltip": "가로 위치 (0 = 왼쪽, 1 = 오른쪽).",
        "config.pos_y.name": "위치 Y (%)",
        "config.pos_y.tooltip": "세로 위치 (0 = 위쪽, 1 = 아래쪽)."
    },
    "pl": {
        "config.section.settings": "Ustawienia",
        "config.enabled.name": "Włączone",
        "config.enabled.tooltip": "Przełącza statyczny pasek łowienia.",
        "config.pos_x.name": "Pozycja X (%)",
        "config.pos_x.tooltip": "Pozycja pozioma (0 = Lewo, 1 = Prawo).",
        "config.pos_y.name": "Pozycja Y (%)",
        "config.pos_y.tooltip": "Pozycja pionowa (0 = Góra, 1 = Dół)."
    },
    "pt-BR": {
        "config.section.settings": "Configurações",
        "config.enabled.name": "Ativado",
        "config.enabled.tooltip": "Alterna a barra de pesca estática.",
        "config.pos_x.name": "Posição X (%)",
        "config.pos_x.tooltip": "Posição horizontal (0 = Esquerda, 1 = Direita).",
        "config.pos_y.name": "Posição Y (%)",
        "config.pos_y.tooltip": "Posição vertical (0 = Topo, 1 = Fundo)."
    },
    "ru": {
        "config.section.settings": "Настройки",
        "config.enabled.name": "Включено",
        "config.enabled.tooltip": "Включить/выключить статическую полосу рыбалки.",
        "config.pos_x.name": "Позиция X (%)",
        "config.pos_x.tooltip": "Горизонтальная позиция (0 = Слева, 1 = Справа).",
        "config.pos_y.name": "Позиция Y (%)",
        "config.pos_y.tooltip": "Вертикальная позиция (0 = Сверху, 1 = Снизу)."
    },
    "tr": {
        "config.section.settings": "Ayarlar",
        "config.enabled.name": "Etkin",
        "config.enabled.tooltip": "Statik balık tutma çubuğunu açar/kapatır.",
        "config.pos_x.name": "Konum X (%)",
        "config.pos_x.tooltip": "Yatay konum (0 = Sol, 1 = Sağ).",
        "config.pos_y.name": "Konum Y (%)",
        "config.pos_y.tooltip": "Dikey konum (0 = Üst, 1 = Alt)."
    },
    "uk": {
        "config.section.settings": "Налаштування",
        "config.enabled.name": "Увімкнено",
        "config.enabled.tooltip": "Перемкнути статичну панель риболовлі.",
        "config.pos_x.name": "Позиція X (%)",
        "config.pos_x.tooltip": "Горизонтальна позиція (0 = Зліва, 1 = Справа).",
        "config.pos_y.name": "Позиція Y (%)",
        "config.pos_y.tooltip": "Вертикальна позиція (0 = Зверху, 1 = Знизу)."
    },
    "zh": {
        "config.section.settings": "设置",
        "config.enabled.name": "启用",
        "config.enabled.tooltip": "切换静态钓鱼条功能。",
        "config.pos_x.name": "位置 X (%)",
        "config.pos_x.tooltip": "水平屏幕位置 (0 = 左, 1 = 右)。",
        "config.pos_y.name": "位置 Y (%)",
        "config.pos_y.tooltip": "垂直屏幕位置 (0 = 上, 1 = 下)。"
    },
    "vi": {
        "config.section.settings": "Cài đặt",
        "config.enabled.name": "Đã bật",
        "config.enabled.tooltip": "Bật/tắt thanh câu cá tĩnh.",
        "config.pos_x.name": "Vị trí X (%)",
        "config.pos_x.tooltip": "Vị trí ngang (0 = Trái, 1 = Phải).",
        "config.pos_y.name": "Vị trí Y (%)",
        "config.pos_y.tooltip": "Vị trí dọc (0 = Trên, 1 = Dưới)."
    }
}

def create_translation_zip(output_filename="i18n_translations.zip"):
    # Create a buffer to hold the zip file in memory
    zip_buffer = io.BytesIO()

    with zipfile.ZipFile(zip_buffer, "w", zipfile.ZIP_DEFLATED) as zip_file:
        for lang, content in translations.items():
            # Define the filename (e.g., zh.json)
            file_name = f"{lang}.json"
            # Convert dictionary to pretty JSON string
            json_str = json.dumps(content, ensure_ascii=False, indent=2)
            # Add to zip
            zip_file.writestr(file_name, json_str)
    
    # Write the buffer to a real file
    with open(output_filename, "wb") as f:
        f.write(zip_buffer.getvalue())
    
    print(f"Successfully created {output_filename} with {len(translations)} files.")

if __name__ == "__main__":
    create_translation_zip()