from pathlib import Path
import subprocess, os, textwrap, json, shutil

OUT = Path('/mnt/data/swyftly_ui_mockups')
OUT.mkdir(parents=True, exist_ok=True)

CSS = r'''
:root{
  --primary:#3A1D32; --primary-dark:#2A1425; --accent:#B76E79; --blush:#F3D9D6;
  --ivory:#FFF9F4; --sand:#F4EDE7; --champagne:#E8D6C7; --text:#1F1A1C; --muted:#6F5E66;
  --success:#0F766E; --warning:#B45309; --error:#B42318; --surface:#FFFFFF;
  --shadow:0 18px 48px rgba(58,29,50,.12); --soft-shadow:0 10px 28px rgba(58,29,50,.10);
}
*{box-sizing:border-box} body{margin:0;background:var(--ivory);font-family:Inter,Segoe UI,Roboto,Arial,sans-serif;color:var(--text);}
.screen{position:relative;overflow:hidden;background:var(--ivory);}
.desktop{width:1440px;height:1000px;} .mobile{width:390px;height:844px;background:var(--ivory);}
.header{height:78px;background:var(--primary);color:#fff;display:flex;align-items:center;padding:0 46px;gap:28px;box-shadow:0 12px 36px rgba(42,20,37,.18)}
.logo{display:flex;align-items:center;gap:11px;font-weight:900;font-size:27px;letter-spacing:-.8px;white-space:nowrap}.logo-mark{width:36px;height:36px;border-radius:12px;background:linear-gradient(135deg,var(--accent),var(--blush));display:grid;place-items:center;color:var(--primary);font-weight:900;box-shadow:inset 0 0 0 1px rgba(255,255,255,.36)}
.nav{display:flex;gap:22px;font-size:14px;opacity:.92;white-space:nowrap}.nav span:first-child{color:var(--blush);font-weight:800}.search{height:44px;background:#fff;border-radius:999px;display:flex;align-items:center;padding:0 17px;color:var(--muted);flex:1;max-width:430px;box-shadow:inset 0 0 0 1px rgba(232,214,199,.55)}
.header-actions{margin-left:auto;display:flex;align-items:center;gap:14px}.pill{border-radius:999px;padding:8px 13px;font-weight:800;font-size:12px;background:var(--blush);color:var(--primary);display:inline-flex;gap:6px;align-items:center}.avatar{width:38px;height:38px;border-radius:50%;background:linear-gradient(135deg,#fff,var(--blush));color:var(--primary);display:grid;place-items:center;font-weight:900}
.btn{border:0;border-radius:14px;padding:12px 18px;font-weight:900;letter-spacing:.1px;background:var(--primary);color:white;box-shadow:0 10px 22px rgba(58,29,50,.18);display:inline-flex;gap:8px;align-items:center;justify-content:center}.btn.secondary{background:#fff;color:var(--primary);border:1px solid var(--champagne);box-shadow:none}.btn.accent{background:var(--accent);color:var(--text)}.btn.success{background:var(--success)}.btn.ghost{background:transparent;color:var(--primary);box-shadow:none;border:1px solid var(--champagne)}.btn.full{width:100%}
.main{padding:34px 46px}.grid{display:grid;gap:24px}.two{grid-template-columns:1.4fr .9fr}.three{grid-template-columns:repeat(3,1fr)}.four{grid-template-columns:repeat(4,1fr)}.five{grid-template-columns:repeat(5,1fr)}
.card{background:#fff;border:1px solid rgba(232,214,199,.8);border-radius:26px;box-shadow:var(--soft-shadow);overflow:hidden}.card.pad{padding:24px}.card.compact{border-radius:20px;padding:18px}.muted{color:var(--muted)}.small{font-size:12px}.eyebrow{text-transform:uppercase;letter-spacing:.13em;font-size:12px;font-weight:900;color:var(--accent)}.h1{font-size:56px;line-height:1;letter-spacing:-2.2px;font-weight:950;margin:14px 0 18px;color:var(--primary)}.h2{font-size:32px;line-height:1.08;letter-spacing:-1px;font-weight:950;margin:0 0 12px;color:var(--primary)}.h3{font-size:20px;font-weight:950;margin:0 0 8px}.row{display:flex;align-items:center;gap:12px}.row.between{justify-content:space-between}.col{display:flex;flex-direction:column;gap:12px}.divider{height:1px;background:var(--champagne);margin:16px 0}.badge{border-radius:999px;background:var(--sand);color:var(--primary);padding:7px 11px;font-weight:900;font-size:12px;display:inline-flex;align-items:center;gap:6px}.badge.green{background:rgba(15,118,110,.12);color:var(--success)}.badge.amber{background:rgba(180,83,9,.12);color:var(--warning)}.badge.red{background:rgba(180,35,24,.12);color:var(--error)}.badge.rose{background:var(--blush);color:var(--primary)}
.hero{height:418px;display:grid;grid-template-columns:1.02fr .98fr;background:radial-gradient(circle at 84% 22%,#F3D9D6 0 25%,transparent 26%),linear-gradient(135deg,#FFF9F4 0%,#F4EDE7 100%);border:1px solid var(--champagne);border-radius:34px;overflow:hidden;box-shadow:var(--shadow)}.hero-copy{padding:50px 52px}.hero-art{position:relative;overflow:hidden}.bubble{position:absolute;border-radius:28px;background:#fff;border:1px solid var(--champagne);box-shadow:var(--shadow)}.bubble.one{width:230px;height:310px;right:260px;top:40px;transform:rotate(-6deg)}.bubble.two{width:230px;height:310px;right:70px;top:78px;transform:rotate(7deg)}.bubble.three{width:172px;height:132px;right:210px;bottom:52px;transform:rotate(2deg)}
.photo{height:100%;border-radius:inherit;position:relative;overflow:hidden;background:linear-gradient(135deg,#ede0d4,#b76e79)}.photo:after{content:'';position:absolute;inset:18px;border:1px solid rgba(255,255,255,.55);border-radius:22px}.dress{background:linear-gradient(150deg,#e6d3cb 0%,#31162a 38%,#5c2a4a 72%,#f3d9d6 100%)}.bag{background:linear-gradient(150deg,#f8ebe1,#b76e79 38%,#3a1d32 100%)}.jewel{background:radial-gradient(circle at 50% 42%,#fff 0 9%,#b76e79 10% 20%,transparent 21%),linear-gradient(135deg,#fff5ed,#d6a18d,#3a1d32)}.beauty{background:linear-gradient(160deg,#f3d9d6,#fff 35%,#b76e79 55%,#3a1d32)}.shoe{background:linear-gradient(150deg,#fff,#e8d6c7 30%,#1f1a1c 78%,#b76e79)}.model{background:linear-gradient(160deg,#fff9f4,#b76e79 32%,#3a1d32 75%)}.photo-label{position:absolute;left:18px;bottom:18px;background:rgba(255,255,255,.84);backdrop-filter:blur(5px);padding:10px 12px;border-radius:14px;font-weight:900;color:var(--primary)}
.product-card{background:#fff;border:1px solid var(--champagne);border-radius:24px;overflow:hidden;box-shadow:0 12px 28px rgba(58,29,50,.09)}.product-img{height:230px;border-radius:22px;margin:10px;position:relative;overflow:hidden}.product-body{padding:0 16px 18px}.price{font-weight:950;color:var(--primary);font-size:18px}.strike{text-decoration:line-through;color:var(--muted);font-size:13px}.rating{color:#B45309;font-weight:900;font-size:12px}.quick{position:absolute;top:12px;right:12px;background:#fff;border-radius:999px;padding:8px 10px;font-weight:900;color:var(--primary);box-shadow:0 8px 18px rgba(0,0,0,.10)}
.sidebar{background:#fff;border:1px solid var(--champagne);border-radius:26px;padding:22px;box-shadow:var(--soft-shadow)}.filter-group{margin:18px 0}.check{display:flex;align-items:center;gap:10px;margin:11px 0;color:var(--muted);font-weight:700}.box{width:18px;height:18px;border-radius:6px;border:1px solid var(--champagne);background:var(--ivory)}.box.on{background:var(--primary);border-color:var(--primary);box-shadow:inset 0 0 0 4px #fff}.chip{display:inline-flex;padding:8px 12px;border-radius:999px;border:1px solid var(--champagne);background:#fff;color:var(--primary);font-weight:800;font-size:12px;margin:4px}.chip.on{background:var(--primary);color:#fff;border-color:var(--primary)}
.pdp{display:grid;grid-template-columns:1.05fr .95fr;gap:30px}.gallery{display:grid;grid-template-columns:105px 1fr;gap:16px}.thumbs{display:flex;flex-direction:column;gap:13px}.thumb{height:98px;border-radius:18px;overflow:hidden;border:2px solid #fff;box-shadow:0 10px 22px rgba(0,0,0,.08)}.main-photo{height:662px;border-radius:32px;overflow:hidden;box-shadow:var(--shadow)}.option{border:1px solid var(--champagne);border-radius:14px;padding:10px 14px;font-weight:900;background:#fff}.option.on{background:var(--primary);color:#fff;border-color:var(--primary)}
.summary-box{background:var(--sand);border-radius:22px;padding:18px;border:1px solid var(--champagne)}.stepper{display:flex;gap:8px;align-items:center}.step{height:8px;flex:1;border-radius:999px;background:var(--champagne)}.step.on{background:var(--primary)}.form-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.field{background:#fff;border:1px solid var(--champagne);border-radius:15px;padding:13px 14px;color:var(--muted);font-weight:750}.order-item{display:grid;grid-template-columns:72px 1fr auto;gap:12px;align-items:center;padding:12px 0;border-bottom:1px solid var(--champagne)}.mini-img{width:72px;height:72px;border-radius:16px;overflow:hidden}.metric{background:#fff;border:1px solid var(--champagne);border-radius:20px;padding:18px;box-shadow:0 8px 18px rgba(58,29,50,.06)}.metric .num{font-size:28px;font-weight:950;color:var(--primary);letter-spacing:-.8px}.table{width:100%;border-collapse:separate;border-spacing:0 10px}.table th{text-align:left;color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.08em;padding:0 12px}.table td{background:#fff;border-top:1px solid var(--champagne);border-bottom:1px solid var(--champagne);padding:14px 12px;font-weight:800}.table td:first-child{border-left:1px solid var(--champagne);border-radius:14px 0 0 14px}.table td:last-child{border-right:1px solid var(--champagne);border-radius:0 14px 14px 0}.side-nav{width:252px;background:var(--primary);height:1000px;color:#fff;padding:28px 22px;position:absolute;left:0;top:0}.side-nav .logo{margin-bottom:34px}.side-nav .nav-item{display:flex;align-items:center;gap:12px;padding:13px 14px;border-radius:14px;color:rgba(255,255,255,.78);font-weight:850;margin:4px 0}.side-nav .nav-item.on{background:rgba(243,217,214,.16);color:#fff}.dashboard{margin-left:252px;padding:30px 34px}.topbar{height:54px;display:flex;align-items:center;justify-content:space-between;margin-bottom:22px}.ai-card{background:linear-gradient(135deg,#fff,#fff7f4);border:1px solid var(--accent);border-radius:24px;box-shadow:0 14px 36px rgba(183,110,121,.14);padding:22px}.quality-ring{width:112px;height:112px;border-radius:50%;background:conic-gradient(var(--success) 0 82%, var(--champagne) 82%);display:grid;place-items:center}.quality-ring span{width:82px;height:82px;border-radius:50%;background:#fff;display:grid;place-items:center;font-weight:950;color:var(--primary)}.chat{display:flex;flex-direction:column;gap:12px}.bubble-msg{max-width:78%;padding:13px 16px;border-radius:20px;font-weight:750;line-height:1.36}.bubble-msg.user{align-self:flex-end;background:var(--primary);color:#fff;border-bottom-right-radius:6px}.bubble-msg.bot{align-self:flex-start;background:#fff;border:1px solid var(--champagne);border-bottom-left-radius:6px}.campaign-card{display:grid;grid-template-columns:74px 1fr auto;gap:14px;align-items:center;background:#fff;border:1px solid var(--champagne);border-radius:20px;padding:14px}.bar{height:10px;border-radius:999px;background:var(--champagne);overflow:hidden}.bar span{display:block;height:100%;border-radius:999px;background:var(--primary)}
/* Mobile */
.mobile .mobile-appbar{height:64px;background:var(--primary);color:#fff;display:flex;align-items:center;justify-content:space-between;padding:0 18px}.mobile .logo{font-size:21px}.mobile .logo-mark{width:31px;height:31px;border-radius:10px}.mobile-content{padding:16px 15px 84px}.mobile .h1{font-size:33px;line-height:1.02;letter-spacing:-1.2px}.mobile .h2{font-size:24px}.mobile .h3{font-size:17px}.mobile .search{height:42px;max-width:none;width:100%;margin:12px 0;background:#fff}.mobile .hero{height:288px;display:block;border-radius:25px}.mobile .hero-copy{padding:24px}.mobile .hero-art{display:none}.mobile .product-card{border-radius:20px}.mobile .product-img{height:166px;border-radius:18px;margin:8px}.mobile .product-body{padding:0 12px 14px}.mobile .two-col{display:grid;grid-template-columns:1fr 1fr;gap:12px}.mobile .scroll-row{display:flex;gap:12px;overflow:hidden}.mobile .scroll-card{min-width:155px}.bottom-nav{position:absolute;left:0;right:0;bottom:0;height:70px;background:#fff;border-top:1px solid var(--champagne);display:grid;grid-template-columns:repeat(5,1fr);z-index:10}.bottom-nav div{display:grid;place-items:center;font-size:11px;font-weight:850;color:var(--muted);padding-top:8px}.bottom-nav .on{color:var(--primary)}.mobile .pdp{display:block}.mobile .main-photo{height:322px;border-radius:24px}.mobile .gallery{display:block}.mobile .thumbs{display:none}.mobile .form-grid{display:block}.mobile .field{margin:10px 0}.mobile .order-item{grid-template-columns:60px 1fr auto}.mobile .mini-img{width:60px;height:60px}.mobile .side-mini{display:flex;gap:10px;overflow:hidden;margin:12px 0}.mobile .table{font-size:12px}.mobile .metric{padding:14px;border-radius:18px}.mobile .metric .num{font-size:21px}.mobile .quality-ring{width:88px;height:88px}.mobile .quality-ring span{width:66px;height:66px}.mobile .campaign-card{grid-template-columns:56px 1fr;gap:10px}.mobile .campaign-card .hide-mobile{display:none}.mobile .admin-list .campaign-card{grid-template-columns:1fr}.mobile .chip{font-size:11px;padding:7px 10px}.mobile .btn{padding:11px 14px;border-radius:13px}.mobile .badge{font-size:11px}.mobile .card.pad{padding:17px;border-radius:22px}.mobile .main{padding:0}.mobile .desktop-only{display:none}.mobile .mobile-tabs{display:flex;gap:8px;overflow:hidden}.mobile .mobile-tabs .chip{white-space:nowrap}.mobile .dashboard{margin:0;padding:0}.mobile .mobile-dashboard-head{padding:16px}.mobile .grid, .mobile .three, .mobile .four, .mobile .five, .mobile .two{display:block}.mobile .card{margin-bottom:14px}.mobile .seller-nav{display:flex;gap:8px;overflow:hidden;margin:10px 0}.mobile .seller-nav .chip.on{background:var(--primary);color:white}.mobile .chat-panel{height:500px;display:flex;flex-direction:column}.mobile .chat{flex:1;overflow:hidden}.mobile .bubble-msg{max-width:90%;font-size:13px}.mobile .summary-box{border-radius:18px;padding:14px}.mobile .photo-label{font-size:12px;padding:8px;left:12px;bottom:12px}.mobile .screen-title{padding:16px 15px 0}.mobile .list-row{background:#fff;border:1px solid var(--champagne);border-radius:18px;padding:13px;margin-bottom:10px}.mobile .side-nav{display:none}
'''
(OUT/'style.css').write_text(CSS)

# Common components

def desktop_header(active='Shop'):
    navs = ['Shop','New In','Beauty','Jewellery','Sellers']
    nav_html = ''.join(f'<span>{n}</span>' for n in navs)
    return f'''<div class="header">
      <div class="logo"><div class="logo-mark">S</div>Swyftly</div>
      <div class="nav">{nav_html}</div>
      <div class="search">⌕ Search dresses, jewellery, beauty, sellers...</div>
      <div class="header-actions"><span class="pill">✨ AI Style Finder</span><span>♡</span><span>🛒</span><div class="avatar">N</div></div>
    </div>'''

def mobile_appbar(title='Swyftly', icon='☰'):
    return f'''<div class="mobile-appbar"><div class="row"><span>{icon}</span><div class="logo"><div class="logo-mark">S</div>{title}</div></div><div class="row"><span>♡</span><span>🛒</span></div></div>'''

def bottom_nav(active='Home'):
    items = [('Home','⌂'),('Search','⌕'),('AI','✨'),('Orders','◴'),('Me','◉')]
    html=''.join(f'<div class="{"on" if name==active else ""}"><span>{icon}</span><span>{name}</span></div>' for name,icon in items)
    return f'<div class="bottom-nav">{html}</div>'

def product_card(cls,title,price,sub='Boutique seller',badge='New',rating='4.8'):
    return f'''<div class="product-card">
      <div class="product-img photo {cls}"><div class="quick">♡</div><div class="photo-label">{badge}</div></div>
      <div class="product-body">
        <div class="row between"><div class="rating">★ {rating}</div><span class="small muted">{sub}</span></div>
        <div class="h3" style="margin-top:8px">{title}</div>
        <div class="row between"><div><span class="price">{price}</span> <span class="strike">R999</span></div><span class="badge rose">AI tagged</span></div>
      </div>
    </div>'''

def side_nav(active='Dashboard', kind='seller'):
    if kind == 'admin':
        items = ['Dashboard','Seller Review','Product Moderation','Orders','Payments','Payouts','Disputes','Support','Settings']
    else:
        items = ['Dashboard','Products','Orders','Inventory','Campaigns','AI Assistant','Payouts','Storefront','Settings']
    html=''.join(f'<div class="nav-item {"on" if i==active else ""}"><span>{"●" if i==active else "○"}</span>{i}</div>' for i in items)
    return f'''<div class="side-nav"><div class="logo"><div class="logo-mark">S</div>Swyftly</div>{html}</div>'''

# Screen builders

def desktop_home():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {desktop_header()}
    <main class="main">
      <section class="hero">
        <div class="hero-copy">
          <div class="eyebrow">AI-powered fashion marketplace</div>
          <div class="h1">Shop local style, beauty & jewellery. Swyftly.</div>
          <p class="muted" style="font-size:18px;line-height:1.55;max-width:560px">Discover curated products from South African boutiques, independent sellers, beauty creators and jewellery makers with secure checkout and buyer protection.</p>
          <div class="row" style="margin-top:28px"><button class="btn">Shop New Arrivals</button><button class="btn secondary">Open a Free Store</button></div>
          <div class="row" style="margin-top:22px"><span class="badge green">Verified sellers</span><span class="badge rose">AI listing quality</span><span class="badge">Fast local delivery</span></div>
        </div>
        <div class="hero-art"><div class="bubble one"><div class="photo dress"><div class="photo-label">Eveningwear</div></div></div><div class="bubble two"><div class="photo bag"><div class="photo-label">Accessories</div></div></div><div class="bubble three"><div class="photo beauty"><div class="photo-label">Beauty picks</div></div></div></div>
      </section>
      <div class="row between" style="margin:30px 0 16px"><div><div class="eyebrow">Featured today</div><div class="h2">Curated drops from local sellers</div></div><button class="btn secondary">View all</button></div>
      <section class="grid four">
        {product_card('dress','Black Satin Midi Dress','R799','Luna Atelier','Limited')}
        {product_card('jewel','Gold Hoop Earrings','R249','Moyo Jewels','Trending')}
        {product_card('beauty','Glow Lip & Cheek Tint','R189','Nude Beauty','Beauty')}
        {product_card('bag','Rose Mini Shoulder Bag','R549','Amara Studio','Featured')}
      </section>
    </main></div></body></html>'''

def desktop_search():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {desktop_header()}
    <main class="main">
      <div class="row between"><div><div class="eyebrow">Search results</div><div class="h2">Black dresses for evening events</div><p class="muted">128 products • Johannesburg, Cape Town and online sellers</p></div><div class="row"><span class="badge rose">Semantic search</span><button class="btn secondary">Sort: Recommended</button></div></div>
      <div style="display:grid;grid-template-columns:280px 1fr;gap:24px;margin-top:20px">
        <aside class="sidebar"><div class="h3">Filters</div><div class="filter-group"><b>Category</b><div class="check"><span class="box on"></span>Dresses</div><div class="check"><span class="box"></span>Jumpsuits</div><div class="check"><span class="box"></span>Sets</div></div><div class="filter-group"><b>Size</b><div><span class="chip on">S</span><span class="chip on">M</span><span class="chip">L</span><span class="chip">XL</span></div></div><div class="filter-group"><b>Occasion</b><div class="check"><span class="box on"></span>Evening</div><div class="check"><span class="box"></span>Wedding guest</div><div class="check"><span class="box"></span>Workwear</div></div><div class="filter-group"><b>Price</b><div class="summary-box">R250 — R1,500<div class="bar" style="margin-top:12px"><span style="width:64%"></span></div></div></div><button class="btn full">Apply filters</button></aside>
        <section class="grid three">
          {product_card('dress','Black Satin Midi Dress','R799','Luna Atelier','AI match')}
          {product_card('model','One-Shoulder Evening Dress','R1,199','Nova Closet','Premium')}
          {product_card('dress','Ruched Party Dress','R649','The Style Room','Sale')}
          {product_card('shoe','Black Block Heels','R599','Step Studio','Pair it')}
          {product_card('jewel','Gold Drop Earrings','R329','Moyo Jewels','Pair it')}
          {product_card('bag','Nude Evening Clutch','R429','Amara Studio','Pair it')}
        </section>
      </div>
    </main></div></body></html>'''

def desktop_product():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {desktop_header()}
    <main class="main">
      <div class="pdp">
        <div class="gallery"><div class="thumbs"><div class="thumb"><div class="photo dress"></div></div><div class="thumb"><div class="photo model"></div></div><div class="thumb"><div class="photo bag"></div></div><div class="thumb"><div class="photo jewel"></div></div></div><div class="main-photo"><div class="photo dress"><div class="photo-label">AI: satin • evening • formal</div></div></div></div>
        <div class="card pad" style="height:662px">
          <div class="row between"><span class="badge green">Verified seller</span><span class="rating">★ 4.9 · 236 reviews</span></div>
          <h1 class="h1" style="font-size:44px;margin-top:20px">Black Satin Midi Evening Dress</h1>
          <p class="muted" style="line-height:1.55;font-size:16px">A sleek black satin midi dress designed for evening events, parties and formal occasions. Soft sheen finish, adjustable straps and flattering drape.</p>
          <div class="row" style="margin:18px 0"><span class="price" style="font-size:31px">R799</span><span class="strike">R999</span><span class="badge red">20% off</span></div>
          <div class="divider"></div>
          <div class="h3">Size</div><div class="row" style="margin:8px 0 18px"><span class="option">S</span><span class="option on">M</span><span class="option">L</span><span class="option">XL</span><span class="badge amber">Only 3 left</span></div>
          <div class="h3">Complete the look</div><div class="grid three" style="gap:12px;margin:10px 0 18px"><div class="summary-box"><b>Gold hoops</b><br><span class="muted">R249</span></div><div class="summary-box"><b>Nude clutch</b><br><span class="muted">R429</span></div><div class="summary-box"><b>Block heels</b><br><span class="muted">R599</span></div></div>
          <div class="summary-box"><div class="row between"><b>Buyer protection included</b><span>🛡</span></div><p class="muted small">Secure payment held until delivery and support for eligible returns.</p></div>
          <div class="row" style="margin-top:20px"><button class="btn" style="flex:1">Add to Cart</button><button class="btn success" style="flex:1">Buy Now</button><button class="btn secondary">♡</button></div>
        </div>
      </div>
    </main></div></body></html>'''

def desktop_checkout():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {desktop_header()}
    <main class="main">
      <div class="row between"><div><div class="eyebrow">Secure checkout</div><div class="h2">Complete your order</div></div><div class="stepper" style="width:420px"><span class="step on"></span><span class="step on"></span><span class="step"></span><span class="step"></span></div></div>
      <div class="grid two" style="margin-top:24px;grid-template-columns:1.1fr .75fr">
        <section class="card pad"><div class="h3">Delivery information</div><div class="form-grid" style="margin-top:14px"><div class="field">First name · Naledi</div><div class="field">Last name · Mokoena</div><div class="field">Phone · +27 82 000 0000</div><div class="field">Email · naledi@swyftly.co.za</div><div class="field" style="grid-column:span 2">Address · 24 Rose Street, Johannesburg</div></div><div class="divider"></div><div class="h3">Shipping method</div><div class="grid two" style="grid-template-columns:1fr 1fr;margin-top:12px"><div class="summary-box"><div class="row between"><b>Standard courier</b><span>R89</span></div><p class="muted small">2–4 business days</p></div><div class="summary-box" style="border-color:var(--primary)"><div class="row between"><b>Pickup point</b><span>R59</span></div><p class="muted small">Pargo / Pudo options</p></div></div><div class="divider"></div><div class="h3">Payment</div><div class="field" style="margin-top:12px">Paystack secure card checkout · **** **** **** 0424</div><div class="row" style="margin-top:22px"><button class="btn success">Pay R1,527 Securely</button><button class="btn secondary">Back to cart</button></div></section>
        <aside class="card pad"><div class="row between"><div class="h3">Order summary</div><span class="badge">Single seller cart</span></div><div class="order-item"><div class="mini-img"><div class="photo dress"></div></div><div><b>Black Satin Midi Dress</b><br><span class="muted small">Size M · Qty 1</span></div><b>R799</b></div><div class="order-item"><div class="mini-img"><div class="photo jewel"></div></div><div><b>Gold Hoop Earrings</b><br><span class="muted small">30mm · Qty 1</span></div><b>R249</b></div><div class="order-item"><div class="mini-img"><div class="photo bag"></div></div><div><b>Nude Evening Clutch</b><br><span class="muted small">Qty 1</span></div><b>R429</b></div><div class="divider"></div><div class="row between"><span>Subtotal</span><b>R1,477</b></div><div class="row between"><span>Shipping</span><b>R59</b></div><div class="row between"><span>Buyer protection</span><b>R0</b></div><div class="divider"></div><div class="row between" style="font-size:22px"><b>Total</b><b>R1,536</b></div><div class="summary-box" style="margin-top:16px"><b>Funds are held safely</b><p class="muted small">Seller payout is released after delivery confirmation and return/dispute checks.</p></div></aside>
      </div>
    </main></div></body></html>'''

def desktop_ai_assistant():
    cards = product_card('dress','Black Satin Midi Dress','R799','Luna Atelier','Outfit 1') + product_card('jewel','Gold Hoop Earrings','R249','Moyo Jewels','Outfit 1') + product_card('bag','Nude Evening Clutch','R429','Amara Studio','Outfit 1')
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {desktop_header()}
    <main class="main">
      <div class="grid two" style="grid-template-columns:.85fr 1.15fr">
        <section class="card pad" style="height:842px"><div class="row between"><div><div class="eyebrow">AI Style Finder</div><div class="h2">Tell Swyftly what you need</div></div><span class="badge rose">✨ AI</span></div><div class="chat" style="margin-top:18px"><div class="bubble-msg bot">Hi Naledi. I can build outfits, find gifts, compare beauty products or search by occasion. What are you shopping for?</div><div class="bubble-msg user">I need an outfit for a wedding under R1,500. I like neutral colours.</div><div class="bubble-msg bot">I found a polished neutral outfit from verified sellers. Total estimate: R1,477 before shipping.</div></div><div class="summary-box" style="margin-top:18px"><b>Extracted intent</b><div style="margin-top:10px"><span class="chip on">Wedding</span><span class="chip on">Budget ≤ R1,500</span><span class="chip on">Neutral colours</span><span class="chip">Size M</span></div></div><div class="field" style="margin-top:18px">Ask: “Find a cheaper bag” or “Make it more formal”</div><button class="btn full" style="margin-top:14px">Update recommendations</button></section>
        <section><div class="row between"><div><div class="eyebrow">Real product recommendations</div><div class="h2">Wedding outfit under budget</div></div><button class="btn secondary">Save AI search</button></div><div class="grid three" style="margin-top:18px">{cards}</div><div class="card pad" style="margin-top:22px"><div class="h3">Why this works</div><p class="muted" style="line-height:1.55">The black satin dress creates a formal base, the nude clutch softens the look, and the gold hoops add a jewellery accent without exceeding the buyer’s budget. All items are in stock and from verified sellers.</p><div class="row"><span class="badge green">R1,477 total</span><span class="badge">3 sellers matched</span><span class="badge rose">AI only used real listings</span></div></div></section>
      </div>
    </main></div></body></html>'''

def desktop_seller_dashboard():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {side_nav('Dashboard','seller')}
    <main class="dashboard"><div class="topbar"><div><div class="eyebrow">Seller dashboard</div><div class="h2">Luna Atelier</div></div><div class="row"><span class="badge green">Verified seller</span><button class="btn">Create product</button><div class="avatar">L</div></div></div>
      <section class="grid four"><div class="metric"><span class="muted small">Gross sales</span><div class="num">R38.2k</div><span class="badge green">+18%</span></div><div class="metric"><span class="muted small">Orders</span><div class="num">142</div><span class="badge">This month</span></div><div class="metric"><span class="muted small">Pending payout</span><div class="num">R8.4k</div><span class="badge amber">On hold</span></div><div class="metric"><span class="muted small">AI quality avg.</span><div class="num">87%</div><span class="badge rose">AI assisted</span></div></section>
      <section class="grid two" style="grid-template-columns:1.1fr .8fr;margin-top:24px"><div class="card pad"><div class="row between"><div class="h3">Recent orders</div><button class="btn secondary">View all</button></div><table class="table"><tr><th>Order</th><th>Buyer</th><th>Status</th><th>Total</th></tr><tr><td>#SW1029</td><td>Naledi M.</td><td><span class="badge amber">Ready to ship</span></td><td>R1,536</td></tr><tr><td>#SW1028</td><td>Amanda K.</td><td><span class="badge green">Delivered</span></td><td>R799</td></tr><tr><td>#SW1027</td><td>Thando R.</td><td><span class="badge">Processing</span></td><td>R1,099</td></tr></table></div><div class="ai-card"><div class="row between"><div><div class="eyebrow">AI opportunity</div><div class="h3">Improve 6 listings</div></div><div class="quality-ring"><span>82%</span></div></div><p class="muted">Add measurements, care instructions and better alt text to raise search quality.</p><button class="btn full">Open AI Listing Assistant</button><div class="divider"></div><div class="row between"><span>Campaign ROAS</span><b>4.2x</b></div><div class="bar" style="margin-top:10px"><span style="width:74%"></span></div></div></section>
      <section class="card pad" style="margin-top:24px"><div class="row between"><div class="h3">Top products</div><button class="btn secondary">Manage inventory</button></div><div class="grid four" style="margin-top:14px">{product_card('dress','Black Satin Midi Dress','R799','In stock','Top')}{product_card('model','Ivory Linen Co-ord','R999','In stock','New')}{product_card('bag','Nude Evening Clutch','R429','Low stock','Low')}{product_card('shoe','Black Block Heels','R599','In stock','Pair it')}</div></section>
    </main></div></body></html>'''

def desktop_product_ai():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {side_nav('Products','seller')}
    <main class="dashboard"><div class="topbar"><div><div class="eyebrow">Create product</div><div class="h2">AI Fashion Product Listing Assistant</div></div><div class="row"><button class="btn secondary">Save draft</button><button class="btn">Submit for review</button></div></div>
      <div class="grid two" style="grid-template-columns:.95fr 1.05fr">
        <section class="card pad"><div class="h3">Seller input</div><p class="muted">Upload images and add short notes. AI will suggest content, attributes and missing fields.</p><div class="grid two" style="grid-template-columns:1fr 1fr;margin-top:16px"><div class="main-photo" style="height:310px;border-radius:24px"><div class="photo dress"><div class="photo-label">Image 1</div></div></div><div><div class="field">Product notes<br><b style="color:var(--text)">Black satin dress, sizes S-L, party wear.</b></div><div class="field">Known material · Satin</div><div class="field">Known colour · Black</div><div class="field">Condition · New</div><button class="btn full" style="margin-top:12px">✨ Generate AI suggestion</button></div></div><div class="divider"></div><div class="h3">Product draft form</div><div class="form-grid"><div class="field">Title · Black Satin Midi Evening Dress</div><div class="field">Category · Women > Dresses</div><div class="field">Price · R799</div><div class="field">Sizes · S, M, L</div><div class="field" style="grid-column:span 2;height:92px">Description · A sleek black satin midi dress designed for evening events...</div></div></section>
        <aside class="ai-card"><div class="row between"><div><div class="eyebrow">AI suggestion</div><div class="h2">Quality score: 82%</div></div><div class="quality-ring"><span>82</span></div></div><div class="summary-box" style="margin-top:16px"><b>Recommended title</b><p>Black Satin Midi Evening Dress</p><button class="btn secondary">Use title</button></div><div class="summary-box" style="margin-top:12px"><b>Suggested attributes</b><div style="margin-top:8px"><span class="chip on">Black</span><span class="chip on">Satin</span><span class="chip on">Evening</span><span class="chip on">Party</span><span class="chip">Formal</span></div></div><div class="summary-box" style="margin-top:12px"><b>Missing details</b><ul class="muted"><li>Exact measurements per size</li><li>Care instructions</li><li>Model height / wearing size</li><li>Stock quantity per size</li></ul></div><div class="summary-box" style="margin-top:12px"><b>Risk flags</b><p class="muted small">No high-risk beauty, counterfeit or unsupported claims detected.</p></div><div class="row" style="margin-top:14px"><button class="btn">Apply all safe suggestions</button><button class="btn ghost">Reject</button></div></aside>
      </div>
    </main></div></body></html>'''

def desktop_ads():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {side_nav('Campaigns','seller')}
    <main class="dashboard"><div class="topbar"><div><div class="eyebrow">Seller ads</div><div class="h2">Campaigns & promoted listings</div></div><button class="btn">Create campaign</button></div>
      <section class="grid four"><div class="metric"><span class="muted small">Ad spend</span><div class="num">R640</div><span class="badge">7 days</span></div><div class="metric"><span class="muted small">Impressions</span><div class="num">18.4k</div><span class="badge green">+26%</span></div><div class="metric"><span class="muted small">Orders</span><div class="num">32</div><span class="badge green">from ads</span></div><div class="metric"><span class="muted small">ROAS</span><div class="num">4.2x</div><span class="badge rose">Strong</span></div></section>
      <div class="grid two" style="grid-template-columns:1.05fr .85fr;margin-top:24px"><section class="card pad"><div class="row between"><div class="h3">Active campaigns</div><span class="badge green">2 live</span></div><div class="campaign-card"><div class="mini-img"><div class="photo dress"></div></div><div><b>Wedding Guest Collection</b><br><span class="muted small">Category: Dresses · Daily budget R100</span><div class="bar" style="margin-top:10px"><span style="width:62%"></span></div></div><div><span class="badge green">Live</span><br><b>R6,420 sales</b></div></div><div class="campaign-card" style="margin-top:12px"><div class="mini-img"><div class="photo bag"></div></div><div><b>Evening Accessories Boost</b><br><span class="muted small">Homepage + category placement</span><div class="bar" style="margin-top:10px"><span style="width:38%"></span></div></div><div><span class="badge amber">Review</span><br><b>Starts Fri</b></div></div><div class="campaign-card" style="margin-top:12px"><div class="mini-img"><div class="photo shoe"></div></div><div><b>Winter Heels Promo</b><br><span class="muted small">Search sponsored row</span><div class="bar" style="margin-top:10px"><span style="width:86%"></span></div></div><div><span class="badge">Ended</span><br><b>3.1x ROAS</b></div></div></section><aside class="ai-card"><div class="eyebrow">AI campaign suggestion</div><div class="h2">Promote your top dress</div><p class="muted">Black Satin Midi Dress has high conversion, strong image quality and low return rate. Suggested budget: R80/day for 5 days.</p><div class="summary-box"><div class="row between"><span>Projected impressions</span><b>8k–12k</b></div><div class="row between"><span>Best placement</span><b>Wedding search</b></div><div class="row between"><span>Eligibility</span><span class="badge green">Approved</span></div></div><button class="btn full" style="margin-top:14px">Create from AI suggestion</button></aside></div>
    </main></div></body></html>'''

def desktop_admin_moderation():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {side_nav('Product Moderation','admin')}
    <main class="dashboard"><div class="topbar"><div><div class="eyebrow">Admin console</div><div class="h2">Product moderation queue</div></div><div class="row"><span class="badge amber">18 need review</span><button class="btn secondary">Export</button></div></div>
      <section class="grid four"><div class="metric"><span class="muted small">Pending products</span><div class="num">18</div><span class="badge amber">Today</span></div><div class="metric"><span class="muted small">AI flagged</span><div class="num">7</div><span class="badge red">Risk</span></div><div class="metric"><span class="muted small">Approved</span><div class="num">142</div><span class="badge green">This week</span></div><div class="metric"><span class="muted small">Avg review time</span><div class="num">12m</div><span class="badge">Target</span></div></section>
      <div class="grid two" style="grid-template-columns:1.2fr .8fr;margin-top:24px"><section class="card pad"><div class="h3">Review list</div><table class="table"><tr><th>Product</th><th>Seller</th><th>Risk</th><th>Status</th></tr><tr><td>Designer Inspired Bag</td><td>Urban Closet</td><td><span class="badge red">Counterfeit phrase</span></td><td>Needs review</td></tr><tr><td>Glow Skin Serum</td><td>Beauty Lab</td><td><span class="badge amber">Medical claim</span></td><td>Needs review</td></tr><tr><td>Gold Hoop Earrings</td><td>Moyo Jewels</td><td><span class="badge amber">Hypoallergenic claim</span></td><td>Needs proof</td></tr><tr><td>Black Satin Dress</td><td>Luna Atelier</td><td><span class="badge green">Low risk</span></td><td>Approve</td></tr></table></section><aside class="ai-card"><div class="row between"><div><div class="eyebrow">Selected review</div><div class="h2">Designer Inspired Bag</div></div><span class="badge red">High risk</span></div><div class="main-photo" style="height:210px;border-radius:22px;margin:12px 0"><div class="photo bag"><div class="photo-label">Submitted image</div></div></div><div class="summary-box"><b>AI review summary</b><p class="muted small">Listing uses “designer inspired” and “luxury look”. Seller has no brand authorization file. Recommend requesting proof or rejecting under counterfeit policy.</p></div><div class="row" style="margin-top:14px"><button class="btn success">Approve</button><button class="btn accent">Request proof</button><button class="btn" style="background:var(--error)">Reject</button></div></aside></div>
    </main></div></body></html>'''

def desktop_finance_payouts():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen desktop">
    {side_nav('Payouts','admin')}
    <main class="dashboard"><div class="topbar"><div><div class="eyebrow">Finance operations</div><div class="h2">Seller balances & payouts</div></div><div class="row"><button class="btn secondary">Reconcile</button><button class="btn">Release approved payouts</button></div></div>
      <section class="grid four"><div class="metric"><span class="muted small">Gross sales</span><div class="num">R842k</div><span class="badge green">Month</span></div><div class="metric"><span class="muted small">Platform fees</span><div class="num">R86k</div><span class="badge rose">Revenue</span></div><div class="metric"><span class="muted small">Pending payouts</span><div class="num">R218k</div><span class="badge amber">Held</span></div><div class="metric"><span class="muted small">Refund reserve</span><div class="num">R32k</div><span class="badge">Protected</span></div></section>
      <div class="grid two" style="grid-template-columns:1.15fr .85fr;margin-top:24px"><section class="card pad"><div class="row between"><div class="h3">Payout queue</div><span class="badge amber">Manual review required</span></div><table class="table"><tr><th>Seller</th><th>Available</th><th>Hold reason</th><th>Action</th></tr><tr><td>Luna Atelier</td><td>R8,420</td><td><span class="badge green">Clear</span></td><td>Release</td></tr><tr><td>Urban Closet</td><td>R12,910</td><td><span class="badge red">Dispute</span></td><td>Hold</td></tr><tr><td>Moyo Jewels</td><td>R4,250</td><td><span class="badge amber">New seller</span></td><td>Review</td></tr><tr><td>Nude Beauty</td><td>R6,840</td><td><span class="badge amber">Return window</span></td><td>Wait</td></tr></table></section><aside class="card pad"><div class="h3">Ledger snapshot</div><div class="summary-box"><div class="row between"><span>Buyer payments received</span><b>R842,000</b></div><div class="row between"><span>Payment processing fees</span><b>R24,900</b></div><div class="row between"><span>Platform transaction fees</span><b>R86,000</b></div><div class="row between"><span>Seller payable</span><b>R731,100</b></div></div><div class="divider"></div><div class="h3">Controls</div><button class="btn full">View ledger entries</button><button class="btn secondary full" style="margin-top:10px">Download finance export</button><div class="summary-box" style="margin-top:14px"><b>Audit requirement</b><p class="muted small">Every payout release requires admin reason, reconciliation check and immutable audit log entry.</p></div></aside></div>
    </main></div></body></html>'''

# Mobile screens

def mobile_home():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Swyftly')}
    <div class="mobile-content"><div class="search">⌕ Search fashion, jewellery, beauty...</div><section class="hero"><div class="hero-copy"><div class="eyebrow">AI marketplace</div><div class="h1">Shop local style. Swyftly.</div><p class="muted">Curated fashion, beauty and jewellery from verified sellers.</p><button class="btn full">Shop new arrivals</button></div></section><div class="row between" style="margin:18px 0 10px"><div class="h3">Featured drops</div><span class="badge rose">AI tagged</span></div><div class="two-col">{product_card('dress','Black Satin Midi Dress','R799','Luna','New')}{product_card('jewel','Gold Hoop Earrings','R249','Moyo','Trend')}</div></div>{bottom_nav('Home')}</div></body></html>'''

def mobile_search():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Search','←')}
    <div class="mobile-content"><div class="search">black dress wedding under R800</div><div class="mobile-tabs"><span class="chip on">Dresses</span><span class="chip on">Black</span><span class="chip">M</span><span class="chip">Under R800</span></div><div class="row between" style="margin:15px 0 10px"><div><div class="h3">AI-matched results</div><span class="muted small">128 products</span></div><button class="btn secondary">Filters</button></div><div class="two-col">{product_card('dress','Black Satin Midi Dress','R799','Luna','AI')}{product_card('model','Ruched Party Dress','R649','Style Room','Sale')}{product_card('shoe','Black Block Heels','R599','Step','Pair')}{product_card('jewel','Gold Drop Earrings','R329','Moyo','Pair')}</div></div>{bottom_nav('Search')}</div></body></html>'''

def mobile_product():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Product','←')}
    <div class="mobile-content"><div class="main-photo"><div class="photo dress"><div class="photo-label">AI: satin · evening</div></div></div><div class="card pad" style="margin-top:14px"><div class="row between"><span class="badge green">Verified</span><span class="rating">★ 4.9</span></div><div class="h2" style="margin-top:12px">Black Satin Midi Evening Dress</div><p class="muted">Sleek black satin midi dress for parties and formal occasions.</p><div class="row"><span class="price" style="font-size:25px">R799</span><span class="strike">R999</span><span class="badge red">20% off</span></div><div class="divider"></div><div class="h3">Size</div><div><span class="option">S</span> <span class="option on">M</span> <span class="option">L</span> <span class="option">XL</span></div><div class="summary-box" style="margin-top:14px"><b>Buyer protection</b><br><span class="muted small">Secure payment and eligible return support.</span></div><div class="row" style="margin-top:14px"><button class="btn" style="flex:1">Add to cart</button><button class="btn success" style="flex:1">Buy</button></div></div></div>{bottom_nav('Search')}</div></body></html>'''

def mobile_checkout():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Checkout','←')}
    <div class="mobile-content"><div class="stepper"><span class="step on"></span><span class="step on"></span><span class="step"></span></div><div class="card pad" style="margin-top:14px"><div class="h3">Delivery</div><div class="field">Naledi Mokoena</div><div class="field">24 Rose Street, Johannesburg</div><div class="summary-box"><div class="row between"><b>Pickup point</b><span>R59</span></div><span class="muted small">2–4 business days</span></div></div><div class="card pad"><div class="h3">Order summary</div><div class="order-item"><div class="mini-img"><div class="photo dress"></div></div><div><b>Black Satin Dress</b><br><span class="muted small">M · Qty 1</span></div><b>R799</b></div><div class="order-item"><div class="mini-img"><div class="photo jewel"></div></div><div><b>Gold Hoops</b><br><span class="muted small">Qty 1</span></div><b>R249</b></div><div class="row between"><span>Total</span><b style="font-size:22px">R1,107</b></div><button class="btn success full" style="margin-top:14px">Pay securely</button></div></div>{bottom_nav('Orders')}</div></body></html>'''

def mobile_ai_assistant():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('AI Finder','←')}
    <div class="mobile-content"><div class="card pad chat-panel"><div class="row between"><div class="h3">AI Style Finder</div><span class="badge rose">✨ AI</span></div><div class="chat" style="margin-top:12px"><div class="bubble-msg bot">What are you shopping for?</div><div class="bubble-msg user">Wedding outfit under R1,500, neutral colours.</div><div class="bubble-msg bot">I found a dress, earrings and clutch totaling R1,477 before shipping.</div></div><div class="field">Ask to refine results...</div></div><div class="row between"><div class="h3">Recommended outfit</div><span class="badge green">R1,477</span></div><div class="two-col">{product_card('dress','Black Satin Dress','R799','Luna','Fit')}{product_card('jewel','Gold Hoops','R249','Moyo','Fit')}</div></div>{bottom_nav('AI')}</div></body></html>'''

def mobile_seller_dashboard():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Seller','☰')}
    <div class="mobile-content"><div class="row between"><div><div class="eyebrow">Luna Atelier</div><div class="h2">Dashboard</div></div><span class="badge green">Verified</span></div><div class="seller-nav"><span class="chip on">Overview</span><span class="chip">Products</span><span class="chip">Orders</span><span class="chip">Payouts</span></div><div class="two-col"><div class="metric"><span class="muted small">Sales</span><div class="num">R38.2k</div></div><div class="metric"><span class="muted small">Orders</span><div class="num">142</div></div></div><div class="ai-card" style="margin-top:14px"><div class="row between"><div><div class="h3">Improve listings</div><span class="muted small">6 products need details</span></div><div class="quality-ring"><span>82</span></div></div><button class="btn full" style="margin-top:12px">Open AI Assistant</button></div><div class="card pad"><div class="h3">Recent orders</div><div class="list-row"><b>#SW1029</b><br><span class="badge amber">Ready to ship</span> <span class="muted small">R1,536</span></div><div class="list-row"><b>#SW1028</b><br><span class="badge green">Delivered</span> <span class="muted small">R799</span></div></div></div>{bottom_nav('Me')}</div></body></html>'''

def mobile_product_ai():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Create product','←')}
    <div class="mobile-content"><div class="main-photo" style="height:220px"><div class="photo dress"><div class="photo-label">Uploaded image</div></div></div><div class="card pad"><div class="h3">Seller notes</div><div class="field">Black satin dress, sizes S-L, party wear.</div><button class="btn full">✨ Generate with AI</button></div><div class="ai-card"><div class="row between"><div><div class="eyebrow">AI suggestion</div><div class="h2">82% quality</div></div><div class="quality-ring"><span>82</span></div></div><div class="summary-box"><b>Title</b><br>Black Satin Midi Evening Dress</div><div style="margin:10px 0"><span class="chip on">Black</span><span class="chip on">Satin</span><span class="chip on">Evening</span></div><div class="summary-box"><b>Missing</b><br><span class="muted small">Measurements, care instructions, stock per size</span></div><button class="btn full" style="margin-top:12px">Apply safe suggestions</button></div></div>{bottom_nav('Me')}</div></body></html>'''

def mobile_ads():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Campaigns','←')}
    <div class="mobile-content"><div class="row between"><div><div class="eyebrow">Seller ads</div><div class="h2">Campaigns</div></div><button class="btn">New</button></div><div class="two-col"><div class="metric"><span class="muted small">ROAS</span><div class="num">4.2x</div></div><div class="metric"><span class="muted small">Spend</span><div class="num">R640</div></div></div><div class="campaign-card"><div class="mini-img"><div class="photo dress"></div></div><div><b>Wedding Guest Collection</b><br><span class="badge green">Live</span><div class="bar" style="margin-top:8px"><span style="width:62%"></span></div></div><div class="hide-mobile"><b>R6,420</b></div></div><div class="campaign-card"><div class="mini-img"><div class="photo bag"></div></div><div><b>Evening Accessories</b><br><span class="badge amber">Review</span><div class="bar" style="margin-top:8px"><span style="width:38%"></span></div></div><div class="hide-mobile"><b>Starts Fri</b></div></div><div class="ai-card"><div class="h3">AI suggestion</div><p class="muted small">Promote your top dress. Suggested budget: R80/day for 5 days.</p><button class="btn full">Create campaign</button></div></div>{bottom_nav('Me')}</div></body></html>'''

def mobile_admin_moderation():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Admin','☰')}
    <div class="mobile-content"><div class="row between"><div><div class="eyebrow">Moderation</div><div class="h2">Review queue</div></div><span class="badge amber">18</span></div><div class="two-col"><div class="metric"><span class="muted small">AI flagged</span><div class="num">7</div></div><div class="metric"><span class="muted small">Approved</span><div class="num">142</div></div></div><div class="admin-list"><div class="campaign-card"><div><b>Designer Inspired Bag</b><br><span class="muted small">Urban Closet</span><br><span class="badge red">Counterfeit phrase</span></div></div><div class="campaign-card"><div><b>Glow Skin Serum</b><br><span class="muted small">Beauty Lab</span><br><span class="badge amber">Medical claim</span></div></div><div class="campaign-card"><div><b>Gold Hoop Earrings</b><br><span class="muted small">Moyo Jewels</span><br><span class="badge amber">Claim needs proof</span></div></div></div><div class="ai-card"><div class="h3">AI summary</div><p class="muted small">“Designer inspired” requires proof or rejection under counterfeit-risk policy.</p><div class="row"><button class="btn success">Approve</button><button class="btn" style="background:var(--error)">Reject</button></div></div></div>{bottom_nav('Me')}</div></body></html>'''

def mobile_finance_payouts():
    return f'''<!doctype html><html><head><link rel="stylesheet" href="style.css"></head><body><div class="screen mobile">
    {mobile_appbar('Finance','←')}
    <div class="mobile-content"><div><div class="eyebrow">Admin finance</div><div class="h2">Payouts</div></div><div class="two-col"><div class="metric"><span class="muted small">Fees</span><div class="num">R86k</div></div><div class="metric"><span class="muted small">Pending</span><div class="num">R218k</div></div></div><div class="card pad"><div class="h3">Payout queue</div><div class="list-row"><b>Luna Atelier</b><br><span class="badge green">Clear</span> <span class="muted small">R8,420</span></div><div class="list-row"><b>Urban Closet</b><br><span class="badge red">Dispute</span> <span class="muted small">R12,910</span></div><div class="list-row"><b>Moyo Jewels</b><br><span class="badge amber">New seller</span> <span class="muted small">R4,250</span></div></div><div class="summary-box"><b>Ledger snapshot</b><div class="row between"><span>Buyer payments</span><b>R842k</b></div><div class="row between"><span>Seller payable</span><b>R731k</b></div><button class="btn full" style="margin-top:12px">View ledger</button></div></div>{bottom_nav('Me')}</div></body></html>'''

screens = {
    'desktop-home': desktop_home,
    'desktop-search': desktop_search,
    'desktop-product-detail': desktop_product,
    'desktop-checkout': desktop_checkout,
    'desktop-ai-style-assistant': desktop_ai_assistant,
    'desktop-seller-dashboard': desktop_seller_dashboard,
    'desktop-ai-product-listing-assistant': desktop_product_ai,
    'desktop-seller-ad-campaigns': desktop_ads,
    'desktop-admin-moderation': desktop_admin_moderation,
    'desktop-admin-finance-payouts': desktop_finance_payouts,
    'mobile-home': mobile_home,
    'mobile-search': mobile_search,
    'mobile-product-detail': mobile_product,
    'mobile-checkout': mobile_checkout,
    'mobile-ai-style-assistant': mobile_ai_assistant,
    'mobile-seller-dashboard': mobile_seller_dashboard,
    'mobile-ai-product-listing-assistant': mobile_product_ai,
    'mobile-seller-ad-campaigns': mobile_ads,
    'mobile-admin-moderation': mobile_admin_moderation,
    'mobile-admin-finance-payouts': mobile_finance_payouts,
}

for name, builder in screens.items():
    (OUT / f'{name}.html').write_text(builder(), encoding='utf-8')

# Render screenshots with Chromium
chrome = shutil.which('chromium') or shutil.which('chromium-browser') or shutil.which('google-chrome')
if chrome:
    for name in screens:
        size = '1440,1000' if name.startswith('desktop') else '390,844'
        html = OUT / f'{name}.html'
        png = OUT / f'{name}.png'
        cmd = [chrome, '--headless', '--no-sandbox', '--disable-gpu', '--hide-scrollbars', f'--window-size={size}', '--force-device-scale-factor=1', f'--screenshot={png}', html.as_uri()]
        subprocess.run(cmd, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)

# Create an index HTML gallery
items = []
for name in screens:
    orient = 'Desktop' if name.startswith('desktop') else 'Mobile'
    title = name.replace('-', ' ').title()
    items.append(f'''<article class="gallery-card"><h2>{title}</h2><p>{orient} screen mockup</p><a href="{name}.png"><img src="{name}.png" /></a></article>''')
index_css = '''
body{font-family:Inter,Segoe UI,Arial,sans-serif;background:#FFF9F4;color:#1F1A1C;margin:0;padding:32px}.intro{max-width:1100px;margin:0 auto 30px}h1{font-size:44px;color:#3A1D32;margin:0 0 8px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:24px;max-width:1300px;margin:0 auto}.gallery-card{background:white;border:1px solid #E8D6C7;border-radius:22px;padding:18px;box-shadow:0 12px 28px rgba(58,29,50,.09)}.gallery-card h2{font-size:18px;color:#3A1D32;margin:0}.gallery-card p{color:#6F5E66}.gallery-card img{width:100%;border-radius:16px;border:1px solid #E8D6C7;display:block}'''
(OUT / 'index.html').write_text(f'''<!doctype html><html><head><title>Swyftly UI Mockups</title><style>{index_css}</style></head><body><div class="intro"><h1>Swyftly high-fidelity UI mockups</h1><p>Desktop and mobile screen concepts for buyer, seller, admin, AI, checkout, finance and advertising flows.</p></div><div class="grid">{''.join(items)}</div></body></html>''', encoding='utf-8')

# Create README
(OUT / 'README.md').write_text('''# Swyftly High-Fidelity UI Mockups\n\nThis mockup pack contains desktop and mobile screen concepts for the Swyftly transactional fashion, jewellery, accessories and beauty marketplace.\n\n## Included screens\n\n- Public home / discovery\n- Search and category results\n- Product detail\n- Checkout\n- Buyer AI Style Finder\n- Seller dashboard\n- AI Fashion Product Listing Assistant\n- Seller advertising campaigns\n- Admin moderation queue\n- Admin finance and payouts\n\nEach screen is available as both an HTML file and a PNG render. Open `index.html` to view the gallery.\n\n## Design system\n\nThe screens use the Luxe Blush palette:\n\n- Deep Plum: `#3A1D32`\n- Dark Plum: `#2A1425`\n- Rose Gold: `#B76E79`\n- Blush: `#F3D9D6`\n- Warm Ivory: `#FFF9F4`\n- Soft Sand: `#F4EDE7`\n- Champagne: `#E8D6C7`\n- Charcoal: `#1F1A1C`\n- Mauve Grey: `#6F5E66`\n- Emerald: `#0F766E`\n- Amber: `#B45309`\n- Deep Red: `#B42318`\n''')

# Zip package
subprocess.run(['bash','-lc',f'cd {OUT.parent} && zip -qr swyftly_ui_mockups.zip swyftly_ui_mockups'], check=True)
print('generated', len(screens), 'screens')
