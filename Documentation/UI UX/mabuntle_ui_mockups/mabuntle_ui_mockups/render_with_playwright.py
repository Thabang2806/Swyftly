from pathlib import Path
from playwright.sync_api import sync_playwright
import subprocess, shutil, os
OUT = Path('/mnt/data/mabuntle_ui_mockups')
css = (OUT/'style.css').read_text(encoding='utf-8')
html_files = sorted([p for p in OUT.glob('*.html') if p.name != 'index.html'])
# remove old screenshots
for p in OUT.glob('*.png'):
    p.unlink()
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path='/usr/bin/chromium', args=['--no-sandbox','--disable-dev-shm-usage','--disable-gpu','--disable-extensions'])
    for html_file in html_files:
        name = html_file.stem
        is_desktop = name.startswith('desktop')
        viewport = {'width':1440,'height':1000} if is_desktop else {'width':390,'height':844}
        page = browser.new_page(viewport=viewport, device_scale_factor=1)
        html = html_file.read_text(encoding='utf-8')
        html = html.replace('<link rel="stylesheet" href="style.css">', f'<style>{css}</style>')
        page.set_content(html, wait_until='load')
        page.screenshot(path=str(OUT / f'{name}.png'), full_page=False)
        page.close()
    browser.close()

# Create an index HTML gallery
items = []
for html_file in html_files:
    name = html_file.stem
    orient = 'Desktop' if name.startswith('desktop') else 'Mobile'
    title = name.replace('-', ' ').title()
    items.append(f'''<article class="gallery-card"><h2>{title}</h2><p>{orient} screen mockup</p><a href="{name}.png"><img src="{name}.png" /></a></article>''')
index_css = '''
body{font-family:Inter,Segoe UI,Arial,sans-serif;background:#FFF9F4;color:#1F1A1C;margin:0;padding:32px}.intro{max-width:1100px;margin:0 auto 30px}h1{font-size:44px;color:#3A1D32;margin:0 0 8px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:24px;max-width:1300px;margin:0 auto}.gallery-card{background:white;border:1px solid #E8D6C7;border-radius:22px;padding:18px;box-shadow:0 12px 28px rgba(58,29,50,.09)}.gallery-card h2{font-size:18px;color:#3A1D32;margin:0}.gallery-card p{color:#6F5E66}.gallery-card img{width:100%;border-radius:16px;border:1px solid #E8D6C7;display:block}'''
(OUT / 'index.html').write_text(f'''<!doctype html><html><head><title>Mabuntle UI Mockups</title><style>{index_css}</style></head><body><div class="intro"><h1>Mabuntle high-fidelity UI mockups</h1><p>Desktop and mobile screen concepts for buyer, seller, admin, AI, checkout, finance and advertising flows.</p></div><div class="grid">{''.join(items)}</div></body></html>''', encoding='utf-8')

# Create README
(OUT / 'README.md').write_text('''# Mabuntle High-Fidelity UI Mockups\n\nThis mockup pack contains desktop and mobile screen concepts for the Mabuntle transactional fashion, jewellery, accessories and beauty marketplace.\n\n## Included screens\n\n- Public home / discovery\n- Search and category results\n- Product detail\n- Checkout\n- Buyer AI Style Finder\n- Seller dashboard\n- AI Fashion Product Listing Assistant\n- Seller advertising campaigns\n- Admin moderation queue\n- Admin finance and payouts\n\nEach screen is available as both an HTML file and a PNG render. Open `index.html` to view the gallery.\n\n## Design system\n\nThe screens use the Luxe Blush palette:\n\n- Deep Plum: `#3A1D32`\n- Dark Plum: `#2A1425`\n- Rose Gold: `#B76E79`\n- Blush: `#F3D9D6`\n- Warm Ivory: `#FFF9F4`\n- Soft Sand: `#F4EDE7`\n- Champagne: `#E8D6C7`\n- Charcoal: `#1F1A1C`\n- Mauve Grey: `#6F5E66`\n- Emerald: `#0F766E`\n- Amber: `#B45309`\n- Deep Red: `#B42318`\n\n## Notes\n\nThese are static mockups intended for design direction and Codex implementation prompts. They are not wired to a backend and use stylised placeholder product imagery.\n''')

# Make desktop/mobile contact sheet images using Pillow
try:
    from PIL import Image, ImageDraw, ImageFont
    def make_board(prefix, outname, thumb_w, cols):
        files = [OUT/f'{p.stem}.png' for p in html_files if p.stem.startswith(prefix)]
        pad=30; label_h=50
        thumbs=[]
        for f in files:
            img=Image.open(f).convert('RGB')
            ratio=thumb_w/img.width
            th=int(img.height*ratio)
            img=img.resize((thumb_w, th), Image.LANCZOS)
            thumbs.append((f.stem, img))
        rows=(len(thumbs)+cols-1)//cols
        cell_h=max(img.height for _,img in thumbs)+label_h
        W=cols*thumb_w+(cols+1)*pad
        H=rows*cell_h+(rows+1)*pad
        board=Image.new('RGB',(W,H),(255,249,244))
        draw=ImageDraw.Draw(board)
        try:
            font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',18)
        except:
            font=None
        for idx,(name,img) in enumerate(thumbs):
            r=idx//cols; c=idx%cols
            x=pad+c*(thumb_w+pad); y=pad+r*(cell_h+pad)
            draw.text((x,y),name.replace('-', ' ').title(),fill=(58,29,50),font=font)
            board.paste(img,(x,y+label_h))
        board.save(OUT/outname, quality=95)
    make_board('desktop','desktop-contact-sheet.jpg',360,3)
    make_board('mobile','mobile-contact-sheet.jpg',190,5)
except Exception as e:
    print('board failed', e)

# Zip package
zip_path = OUT.parent / 'mabuntle_ui_mockups.zip'
if zip_path.exists(): zip_path.unlink()
subprocess.run(['bash','-lc',f'cd {OUT.parent} && zip -qr {zip_path.name} {OUT.name}'], check=True)
print(f'Rendered {len(html_files)} screens to {OUT}. Zip: {zip_path}')
