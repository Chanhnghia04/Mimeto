using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class DiceBetStation : MonoBehaviour, IInteractable
{
    public bool isOpen = false;
    private PlayerInventory _inventory;

    // ── UNDERGROUND STREET CRAPS Palette ──────────────────────────────────────
    private static readonly Color CONCRETE    = new Color(0.18f, 0.17f, 0.16f);
    private static readonly Color CONCRETE_DK = new Color(0.08f, 0.07f, 0.06f);
    private static readonly Color GRAFFITI_R  = new Color(0.90f, 0.12f, 0.08f);
    private static readonly Color GRAFFITI_Y  = new Color(1.00f, 0.85f, 0.05f);
    private static readonly Color GRAFFITI_B  = new Color(0.10f, 0.50f, 1.00f);
    private static readonly Color FELT_GREEN  = new Color(0.08f, 0.28f, 0.12f);
    private static readonly Color CHALK_WHITE = new Color(0.92f, 0.90f, 0.85f);
    private static readonly Color CHALK_DIM   = new Color(0.55f, 0.53f, 0.50f);
    private static readonly Color WIN_G       = new Color(0.30f, 1.00f, 0.40f);
    private static readonly Color LOSE_R      = new Color(1.00f, 0.20f, 0.15f);
    private static readonly Color PUSH_Y      = new Color(1.00f, 0.88f, 0.20f);
    private static readonly Color SPOTLIGHT   = new Color(1.00f, 0.96f, 0.80f);

    // ── State ─────────────────────────────────────────────────────────────────
    enum State { Idle, Rolling, Result }
    private State  _state    = State.Idle;
    private int    _bet      = 10;
    private int[]  _pDice    = new int[2]{1,1};
    private int[]  _dDice    = new int[2]{1,1};
    private float[] _pAnim   = new float[2]{1,1};
    private float[] _dAnim   = new float[2]{1,1};
    private float[] _pStop   = new float[2];
    private float[] _dStop   = new float[2];
    private bool[]  _pStopped= new bool[2];
    private bool[]  _dStopped= new bool[2];
    private float   _rollT;
    private string  _msg     = "PLACE YA BET  —  ROLL THE BONES";
    private bool    _win;

    // ── Dot layout per face ───────────────────────────────────────────────────
    private static readonly Vector2[][] DOTS = {
        new[]{ new Vector2(.50f,.50f) },
        new[]{ new Vector2(.28f,.28f), new Vector2(.72f,.72f) },
        new[]{ new Vector2(.28f,.28f), new Vector2(.50f,.50f), new Vector2(.72f,.72f) },
        new[]{ new Vector2(.28f,.28f), new Vector2(.72f,.28f), new Vector2(.28f,.72f), new Vector2(.72f,.72f) },
        new[]{ new Vector2(.28f,.28f), new Vector2(.72f,.28f), new Vector2(.50f,.50f), new Vector2(.28f,.72f), new Vector2(.72f,.72f) },
        new[]{ new Vector2(.28f,.25f), new Vector2(.72f,.25f), new Vector2(.28f,.50f), new Vector2(.72f,.50f), new Vector2(.28f,.75f), new Vector2(.72f,.75f) },
    };

    // ── Anim ──────────────────────────────────────────────────────────────────
    private float _alpha  = 0f;
    private float _scale  = 0.84f;
    private float _flash  = 0f;
    private float _shakeT = 0f;   // dice shake animation
    private float _diceRattle = 0f;

    private struct Chip { public Vector2 pos, vel; public float life, max; public Color col; public float sz; }
    private List<Chip> _chips = new List<Chip>();

    // ── Textures ──────────────────────────────────────────────────────────────
    private Texture2D _wh, _overlay, _feltTex, _concreteTex, _noiseTex;

    void Awake()
    {
        _wh = Texture2D.whiteTexture;
        _overlay = Mk(new Color(0.03f, 0.02f, 0.01f, 0.95f));

        // Concrete tile noise
        _concreteTex = new Texture2D(8, 8); _concreteTex.filterMode = FilterMode.Point;
        for (int y=0;y<8;y++) for(int x=0;x<8;x++) {
            float n = Random.Range(0f,0.06f);
            _concreteTex.SetPixel(x,y,new Color(0.14f+n,0.13f+n,0.12f+n,1f)); }
        _concreteTex.Apply();

        // Felt noise
        _feltTex = new Texture2D(4,4); _feltTex.filterMode = FilterMode.Point;
        for(int y=0;y<4;y++) for(int x=0;x<4;x++) {
            float n=Random.Range(0f,0.04f);
            _feltTex.SetPixel(x,y,new Color(0.07f+n,0.26f+n,0.11f+n,1f)); }
        _feltTex.Apply();
    }

    Texture2D Mk(Color c) { var t=new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }

    public void Interact(GameObject interactor)
    {
        if (isOpen) return;
        var inv = interactor.GetComponentInParent<PlayerInventory>()
               ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null) return;
        _inventory = inv;
        isOpen = true;
        _alpha=0f; _scale=0.84f; _flash=0f; _shakeT=0f;
        _state=State.Idle; _win=false;
        _msg="PLACE YA BET  —  ROLL THE BONES";
        _chips.Clear();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseStation()
    {
        isOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        if (!isOpen) return;

        // ── Enforce cursor every frame ─────────────────────────────────────
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        float dt = Time.unscaledDeltaTime;
        _alpha  = Mathf.Lerp(_alpha, 1f, dt*10f);
        _scale  = Mathf.Lerp(_scale, 1f, dt*9f);
        _flash  = Mathf.Max(0f, _flash - dt*1.6f);
        _shakeT = Mathf.Max(0f, _shakeT - dt*2.5f);
        _diceRattle += dt;

        if (_state == State.Rolling) DoRoll(dt);

        for (int i=_chips.Count-1;i>=0;i--) {
            var c=_chips[i]; c.life-=dt; c.pos+=c.vel*dt; c.vel+=new Vector2(0,100f)*dt;
            _chips[i]=c; if(c.life<=0f) _chips.RemoveAt(i); }

        if (Input.GetKeyDown(KeyCode.Escape)) CloseStation();
    }

    void StartRoll()
    {
        if (_inventory.credits < _bet || _bet < 10) { _msg="!! NOT ENOUGH COINS, HOMIE !!"; return; }
        _inventory.SpendCredits(_bet);
        _state=State.Rolling; _win=false; _shakeT=1f;
        _rollT=Time.unscaledTime;
        _msg="ROLLIN' . . .";
        for(int i=0;i<2;i++) {
            _pDice[i]=Random.Range(1,7); _dDice[i]=Random.Range(1,7);
            _pAnim[i]=1f; _dAnim[i]=1f;
            _pStopped[i]=false; _dStopped[i]=false;
            _pStop[i]=1.0f+i*0.45f; _dStop[i]=1.25f+i*0.45f;
        }
    }

    void DoRoll(float dt)
    {
        float el = Time.unscaledTime - _rollT;
        bool done = true;
        for(int i=0;i<2;i++) {
            if(!_pStopped[i]) { _pAnim[i]=Mathf.PingPong(el*14f+i*1.7f,5f)+1f;
                if(el>=_pStop[i]){_pAnim[i]=_pDice[i];_pStopped[i]=true;} else done=false; }
            if(!_dStopped[i]) { _dAnim[i]=Mathf.PingPong(el*13f+i*2.3f,5f)+1f;
                if(el>=_dStop[i]){_dAnim[i]=_dDice[i];_dStopped[i]=true;} else done=false; }
        }
        if(done) Evaluate();
    }

    void Evaluate()
    {
        _state=State.Result;
        int pt=_pDice[0]+_pDice[1], dt2=_dDice[0]+_dDice[1];
        bool doubles = _pDice[0]==_pDice[1];
        if(pt>dt2) {
            int pay = doubles ? Mathf.RoundToInt(_bet*2.5f) : _bet*2;
            _inventory.AddCredits(pay); _win=true; _flash=1f;
            _msg=doubles ? $"DOUBLE DOWN! ×2.5 = +{pay} EC 🔥" : $"YOU WIN! {pt} vs {dt2} = +{pay} EC";
            ThrowChips();
        } else if(pt<dt2) {
            _win=false; _msg=$"DEALER WINS  {dt2} vs {pt}  —  Tough break";
        } else {
            _inventory.AddCredits(_bet); _win=false;
            _msg=$"PUSH  {pt}={dt2}  —  Bet back in ya pocket";
        }
    }

    void ThrowChips()
    {
        Color[] cols={GRAFFITI_Y,WIN_G,CHALK_WHITE,GRAFFITI_R,GRAFFITI_B};
        float cx=Screen.width*.5f,cy=Screen.height*.5f;
        for(int i=0;i<65;i++) {
            float a=Random.Range(0f,Mathf.PI*2f),sp=Random.Range(70f,400f);
            _chips.Add(new Chip{
                pos=new Vector2(cx,cy),
                vel=new Vector2(Mathf.Cos(a)*sp,Mathf.Sin(a)*sp-200f),
                life=Random.Range(0.6f,1.5f),max=1.5f,
                col=cols[Random.Range(0,cols.Length)],
                sz=Random.Range(6f,14f)});
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  O N G U I  —  UNDERGROUND STREET CRAPS
    // ═══════════════════════════════════════════════════════════════════════
    void OnGUI()
    {
        if(!isOpen||_inventory==null) return;
        GUI.depth = -10;

        float sw=Screen.width, sh=Screen.height, t=Time.unscaledTime;

        // ── Overlay ────────────────────────────────────────────────────────
        GUI.color=new Color(1,1,1,_alpha);
        GUI.DrawTexture(new Rect(0,0,sw,sh),_overlay);

        // Spotlight from top center
        GUI.color=new Color(SPOTLIGHT.r,SPOTLIGHT.g,SPOTLIGHT.b, 0.04f*_alpha);
        GUI.DrawTexture(new Rect(sw*.25f, 0, sw*.5f, sh*.6f), _wh);

        // Chips
        foreach(var c in _chips) {
            float a=Mathf.Clamp01(c.life/c.max);
            GUI.color=new Color(c.col.r,c.col.g,c.col.b,a*_alpha);
            GUI.DrawTexture(new Rect(c.pos.x-c.sz*.5f,c.pos.y-c.sz*.5f,c.sz,c.sz),_wh);
        }

        // ── Panel ─────────────────────────────────────────────────────────
        float pw=Mathf.Min(sw*.65f,720f)*_scale;
        float ph=Mathf.Min(sh*.82f,590f)*_scale;
        float px=(sw-pw)*.5f, py=(sh-ph)*.5f;

        // Concrete wall bg with noise
        GUI.color=new Color(1,1,1,_alpha);
        GUI.DrawTextureWithTexCoords(new Rect(px-16,py-16,pw+32,ph+32),_concreteTex,
            new Rect(0,0,(pw+32)/8f,(ph+32)/8f));

        // Shake effect when rolling
        float shakeX=0f,shakeY=0f;
        if(_shakeT>0f) { shakeX=Mathf.Sin(_diceRattle*55f)*4f*_shakeT; shakeY=Mathf.Sin(_diceRattle*47f)*3f*_shakeT; }
        px+=shakeX; py+=shakeY;

        // Panel concrete body
        GUI.color=new Color(CONCRETE_DK.r,CONCRETE_DK.g,CONCRETE_DK.b,0.96f*_alpha);
        GUI.DrawTexture(new Rect(px,py,pw,ph),_wh);

        // Graffiti stripe on left edge
        GUI.color=new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b,0.7f*_alpha);
        GUI.DrawTexture(new Rect(px,py,5f,ph),_wh);
        GUI.color=new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,0.7f*_alpha);
        GUI.DrawTexture(new Rect(px+5f,py,4f,ph),_wh);
        GUI.color=new Color(GRAFFITI_B.r,GRAFFITI_B.g,GRAFFITI_B.b,0.6f*_alpha);
        GUI.DrawTexture(new Rect(px+9f,py,3f,ph),_wh);

        // Right edge stripe (mirror)
        GUI.color=new Color(GRAFFITI_B.r,GRAFFITI_B.g,GRAFFITI_B.b,0.6f*_alpha);
        GUI.DrawTexture(new Rect(px+pw-12f,py,3f,ph),_wh);
        GUI.color=new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,0.7f*_alpha);
        GUI.DrawTexture(new Rect(px+pw-9f,py,4f,ph),_wh);
        GUI.color=new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b,0.7f*_alpha);
        GUI.DrawTexture(new Rect(px+pw-5f,py,5f,ph),_wh);

        // ── Header ─────────────────────────────────────────────────────────
        float hh=72f;
        GUI.color=new Color(0.06f,0.05f,0.04f,_alpha);
        GUI.DrawTexture(new Rect(px+12f,py,pw-24f,hh),_wh);

        // Chalk underline
        GUI.color=new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.25f*_alpha);
        GUI.DrawTexture(new Rect(px+12f,py+hh-3f,pw-24f,3f),_wh);

        // Title — chalk-on-wall style
        float tilt = Mathf.Sin(t*.6f)*.5f; // subtle
        var ts=Sty(36,FontStyle.Bold,new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,_alpha),TextAnchor.MiddleCenter);
        // Rough chalk outline (offset copies)
        GUI.color=new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.08f*_alpha);
        GUI.Label(new Rect(px+13f,py+2f,pw-24f,hh),"🎲  DICE DUEL  🎲",ts);
        GUI.Label(new Rect(px+11f,py-1f,pw-24f,hh),"🎲  DICE DUEL  🎲",ts);
        // Main
        GUI.color=new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.92f*_alpha);
        GUI.Label(new Rect(px+12f,py,pw-24f,hh),"🎲  DICE DUEL  🎲",ts);

        // Graffiti tag sub
        var gr=Sty(12,FontStyle.Bold,new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,0.55f*_alpha),TextAnchor.MiddleCenter);
        GUI.color=new Color(1,1,1,_alpha);
        GUI.Label(new Rect(px+12f,py+hh-22f,pw-24f,20f),"HIT BIGGER OR GO HOME",gr);

        // Coins display (chalk style)
        var coins=Sty(15,FontStyle.Bold,new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,_alpha),TextAnchor.MiddleLeft);
        GUI.Label(new Rect(px+20f,py+14f,180f,26f),$"$ {_inventory.credits} EC",coins);

        DrawCloseBtn(px+pw-46f,py+18f);

        // ── Felt craps table ────────────────────────────────────────────────
        float feltX=px+14f, feltY=py+hh+8f;
        float feltW=pw-28f, feltH=ph-hh-108f;

        GUI.color=new Color(1,1,1,_alpha);
        GUI.DrawTextureWithTexCoords(new Rect(feltX,feltY,feltW,feltH),_feltTex,
            new Rect(0,0,feltW/4f,feltH/4f));

        // Felt border (worn chalk line)
        GUI.color=new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.18f*_alpha);
        DrawBorder(feltX+6,feltY+6,feltW-12,feltH-12,2f);
        DrawBorder(feltX+10,feltY+10,feltW-20,feltH-20,1f);

        // ── Status ────────────────────────────────────────────────────────
        DrawStatusBanner(px+14f, feltY+10f, pw-28f);

        // ── Dice layout ───────────────────────────────────────────────────
        float diceSize = Mathf.Min(feltW*.145f, 82f);
        float midX = feltX + feltW*.5f;
        float midY = feltY + feltH*.5f;
        float gap   = diceSize * 0.28f;

        // Chalk dividing line + VS
        GUI.color=new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.15f*_alpha);
        GUI.DrawTexture(new Rect(feltX+18f,midY-1f,feltW-36f,2f),_wh);
        var vs=Sty(16,FontStyle.Bold,new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.35f*_alpha),TextAnchor.MiddleCenter);
        GUI.color=new Color(1,1,1,_alpha);
        GUI.Label(new Rect(midX-30f,midY-12f,60f,24f),"VS",vs);

        // Player section (bottom)
        float pDiceY = midY + 14f;
        float pDiceX = midX - diceSize - gap*.5f;
        DrawSideTag(feltX, pDiceY-28f, feltW*.5f, "YOU", GRAFFITI_B, false);
        DrawDice(pDiceX,              pDiceY, diceSize, Mathf.Clamp(Mathf.RoundToInt(_pAnim[0]),1,6), false, _pStopped[0]);
        DrawDice(pDiceX+diceSize+gap, pDiceY, diceSize, Mathf.Clamp(Mathf.RoundToInt(_pAnim[1]),1,6), false, _pStopped[1]);
        DrawTotalChalk(pDiceX, pDiceY+diceSize+6f, diceSize*2+gap, _pDice[0]+_pDice[1], _pStopped[0]&&_pStopped[1], false);

        // Dealer section (top)
        float dDiceY = midY - diceSize - 42f;
        float dDiceX = midX - diceSize - gap*.5f;
        DrawSideTag(feltX+feltW*.5f, dDiceY-28f, feltW*.5f, "DEALER", GRAFFITI_R, true);
        DrawDice(dDiceX,              dDiceY, diceSize, Mathf.Clamp(Mathf.RoundToInt(_dAnim[0]),1,6), true, _dStopped[0]);
        DrawDice(dDiceX+diceSize+gap, dDiceY, diceSize, Mathf.Clamp(Mathf.RoundToInt(_dAnim[1]),1,6), true, _dStopped[1]);
        DrawTotalChalk(dDiceX, dDiceY+diceSize+6f, diceSize*2+gap, _dDice[0]+_dDice[1], _dStopped[0]&&_dStopped[1], true);

        // Doubles tip (chalk scrawl bottom of felt)
        var tip=Sty(10,FontStyle.Normal,new Color(CHALK_WHITE.r,CHALK_WHITE.g,CHALK_WHITE.b,0.30f*_alpha),TextAnchor.MiddleCenter);
        GUI.Label(new Rect(feltX,feltY+feltH-22f,feltW,18f),"Roll doubles (matching dice) = ×2.5 bonus payout",tip);

        // ── Controls ──────────────────────────────────────────────────────
        float ctrlY=py+ph-100f;
        GUI.color=new Color(0.05f,0.04f,0.03f,0.95f*_alpha);
        GUI.DrawTexture(new Rect(px,ctrlY,pw,100f),_wh);
        GUI.color=new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,0.4f*_alpha);
        GUI.DrawTexture(new Rect(px,ctrlY,pw,2f),_wh);

        float cy2=ctrlY+30f;

        // Bet display
        var bl=Sty(12,FontStyle.Bold,new Color(CHALK_DIM.r,CHALK_DIM.g,CHALK_DIM.b,0.7f*_alpha),TextAnchor.MiddleLeft);
        GUI.color=new Color(1,1,1,_alpha); GUI.Label(new Rect(px+20f,cy2-4f,80f,18f),"BET",bl);
        var ba=Sty(26,FontStyle.Bold,new Color(GRAFFITI_Y.r,GRAFFITI_Y.g,GRAFFITI_Y.b,_alpha),TextAnchor.MiddleLeft);
        GUI.Label(new Rect(px+20f,cy2+10f,130f,34f),$"{_bet} EC",ba);

        bool idle=_state==State.Idle||_state==State.Result;
        float bx=px+135f;
        if(StreetBtn(bx,      cy2+4f,46f,38f,"MIN",GRAFFITI_R,false)&&idle) _bet=10;
        if(StreetBtn(bx+50f,  cy2+4f,46f,38f,"−10",GRAFFITI_R,false)&&idle) _bet=Mathf.Max(10,_bet-10);
        if(StreetBtn(bx+100f, cy2+4f,46f,38f,"+10",WIN_G,     false)&&idle) _bet+=10;
        if(StreetBtn(bx+150f, cy2+4f,46f,38f,"1/2",GRAFFITI_Y,false)&&idle) _bet=Mathf.Max(10,_bet/2);
        if(StreetBtn(bx+200f, cy2+4f,54f,38f,"MAX",GRAFFITI_Y,false)&&idle) _bet=Mathf.Max(10,_inventory.credits);

        bool can=idle&&_inventory.credits>=_bet&&_bet>=10;
        Color rc=can?WIN_G:CHALK_DIM;
        string rl=idle?"🎲  ROLL  🎲":"Rollin'...";
        if(StreetBtn(px+pw-230f,cy2,200f,52f,rl,rc,true)&&can) StartRoll();

        GUI.color=Color.white;
    }

    void DrawSideTag(float x, float y, float w, string label, Color col, bool right)
    {
        var s=Sty(15,FontStyle.Bold,new Color(col.r,col.g,col.b,_alpha),right?TextAnchor.MiddleRight:TextAnchor.MiddleLeft);
        GUI.color=new Color(col.r,col.g,col.b,0.15f*_alpha);
        GUI.DrawTexture(new Rect(x+(right?w*0.4f:0),y,w*(right?0.6f:0.6f),22f),_wh);
        GUI.color=new Color(1,1,1,_alpha);
        // Chalk offset for rough look
        s.normal.textColor=new Color(col.r,col.g,col.b,0.25f*_alpha);
        GUI.Label(new Rect(x+1,y+1,w,22f),label,s);
        s.normal.textColor=new Color(col.r,col.g,col.b,_alpha);
        GUI.Label(new Rect(x,y,w,22f),label,s);
    }

    void DrawDice(float x, float y, float sz, int face, bool isDealer, bool stopped)
    {
        face=Mathf.Clamp(face,1,6);

        // Shake anim offset
        float ox=0f,oy=0f;
        if(!stopped&&_shakeT>0f) { ox=Mathf.Sin(_diceRattle*60f+x)*6f*_shakeT; oy=Mathf.Sin(_diceRattle*55f+y)*5f*_shakeT; }

        float dx=x+ox, dy=y+oy;

        // Long shadow (street style)
        GUI.color=new Color(0,0,0,0.45f*_alpha);
        GUI.DrawTexture(new Rect(dx+7,dy+8,sz,sz),_wh);

        // Win flash aura
        if(stopped&&_flash>0f&&_win) {
            GUI.color=new Color(WIN_G.r,WIN_G.g,WIN_G.b,_flash*0.4f*_alpha);
            GUI.DrawTexture(new Rect(dx-6,dy-6,sz+12,sz+12),_wh);
        }

        // Dice body — white for player, dark red for dealer
        Color faceCol = isDealer ? new Color(0.75f,0.10f,0.06f) : CHALK_WHITE;
        Color dotCol  = isDealer ? CHALK_WHITE : new Color(0.12f,0.08f,0.06f);
        Color borderC = stopped
            ? (isDealer ? new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b,_alpha)
                        : new Color(GRAFFITI_B.r,GRAFFITI_B.g,GRAFFITI_B.b,_alpha))
            : new Color(CHALK_DIM.r,CHALK_DIM.g,CHALK_DIM.b,0.6f*_alpha);

        // 3D bevel (highlight top-left, shadow bottom-right)
        GUI.color=new Color(1,1,1,0.3f*_alpha);
        GUI.DrawTexture(new Rect(dx,dy,sz,4f),_wh);
        GUI.DrawTexture(new Rect(dx,dy,4f,sz),_wh);
        GUI.color=new Color(0,0,0,0.35f*_alpha);
        GUI.DrawTexture(new Rect(dx,dy+sz-4f,sz,4f),_wh);
        GUI.DrawTexture(new Rect(dx+sz-4f,dy,4f,sz),_wh);

        // Face
        GUI.color=new Color(faceCol.r,faceCol.g,faceCol.b,_alpha);
        GUI.DrawTexture(new Rect(dx+2,dy+2,sz-4,sz-4),_wh);

        // Border
        GUI.color=borderC; DrawBorder(dx-2,dy-2,sz+4,sz+4,2.5f);

        // Dots
        float dotR=sz*0.09f;
        Vector2[] dotPos=DOTS[face-1];
        foreach(var dp in dotPos) {
            float ddx=dx+dp.x*sz-dotR, ddy=dy+dp.y*sz-dotR;
            // dot shadow
            GUI.color=new Color(0,0,0,0.25f*_alpha);
            GUI.DrawTexture(new Rect(ddx+1.5f,ddy+1.5f,dotR*2,dotR*2),_wh);
            // dot
            GUI.color=new Color(dotCol.r,dotCol.g,dotCol.b,_alpha);
            GUI.DrawTexture(new Rect(ddx,ddy,dotR*2,dotR*2),_wh);
        }
    }

    void DrawTotalChalk(float x, float y, float w, int total, bool show, bool isDealer)
    {
        if(!show) return;
        Color col=isDealer?new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b):GRAFFITI_B;
        var s=Sty(18,FontStyle.Bold,new Color(col.r,col.g,col.b,_alpha),TextAnchor.MiddleCenter);
        // chalk rough
        s.normal.textColor=new Color(col.r,col.g,col.b,0.2f*_alpha);
        GUI.color=new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x+1,y+1,w,26f),$"= {total}",s);
        s.normal.textColor=new Color(col.r,col.g,col.b,_alpha);
        GUI.Label(new Rect(x,y,w,26f),$"= {total}",s);
    }

    void DrawStatusBanner(float x, float y, float w)
    {
        Color sc = _win&&_state==State.Result ? WIN_G
                 : _state==State.Rolling      ? GRAFFITI_Y
                 : _msg.Contains("PUSH")      ? PUSH_Y
                 : _msg.Contains("WINS")      ? LOSE_R
                 :                              CHALK_WHITE;
        float pulse=_state==State.Result?(Mathf.Sin(Time.unscaledTime*4.5f)+1f)*.2f+0.8f:1f;

        GUI.color=new Color(sc.r,sc.g,sc.b,0.1f*_alpha);
        GUI.DrawTexture(new Rect(x+30f,y,w-60f,26f),_wh);
        var ss=Sty(15,FontStyle.Bold,new Color(sc.r,sc.g,sc.b,_alpha*pulse),TextAnchor.MiddleCenter);
        // chalk double pass
        ss.normal.textColor=new Color(sc.r,sc.g,sc.b,0.3f*_alpha);
        GUI.color=new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x+1,y+1,w,26f),_msg,ss);
        ss.normal.textColor=new Color(sc.r,sc.g,sc.b,_alpha*pulse);
        GUI.Label(new Rect(x,y,w,26f),_msg,ss);
    }

    void DrawCloseBtn(float x, float y)
    {
        Rect r=new Rect(x,y,34f,34f);
        Vector2 mp=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        bool hov=r.Contains(mp);
        GUI.color=new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b,(hov?0.9f:0.3f)*_alpha);
        GUI.DrawTexture(new Rect(x-1,y-1,36f,36f),_wh);
        GUI.color=new Color(0.06f,0.05f,0.04f,_alpha); GUI.DrawTexture(r,_wh);
        var xs=Sty(20,FontStyle.Bold,hov?CHALK_WHITE:new Color(GRAFFITI_R.r,GRAFFITI_R.g,GRAFFITI_R.b,0.7f),TextAnchor.MiddleCenter);
        GUI.color=new Color(1,1,1,_alpha); GUI.Label(r,"✕",xs);
        if(hov&&Event.current.type==EventType.MouseDown&&Event.current.button==0){Event.current.Use();CloseStation();}
    }

    bool StreetBtn(float x, float y, float w, float h, string txt, Color col, bool large)
    {
        Rect r=new Rect(x,y,w,h);
        Vector2 mp=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        bool hov=r.Contains(mp);

        // Spray paint shadow
        GUI.color=new Color(col.r,col.g,col.b,0.08f*_alpha);
        GUI.DrawTexture(new Rect(x-4,y-4,w+8,h+8),_wh);
        // Outline
        GUI.color=new Color(col.r,col.g,col.b,(hov?0.9f:0.4f)*_alpha);
        GUI.DrawTexture(new Rect(x-1.5f,y-1.5f,w+3,h+3),_wh);
        // Body
        GUI.color=new Color(col.r*0.10f,col.g*0.10f,col.b*0.10f,0.92f*_alpha);
        GUI.DrawTexture(r,_wh);
        // Chalk texture top
        GUI.color=new Color(1,1,1,(hov?0.12f:0.04f)*_alpha);
        GUI.DrawTexture(new Rect(x+1,y+1,w-2,h*.35f),_wh);

        int fs=large?18:14;
        Color tc=hov?CHALK_WHITE:new Color(col.r,col.g,col.b,0.9f);
        var s=Sty(fs,FontStyle.Bold,new Color(0,0,0,0.4f*_alpha),TextAnchor.MiddleCenter);
        GUI.color=new Color(1,1,1,_alpha); GUI.Label(new Rect(x+1,y+2,w,h),txt,s);
        s.normal.textColor=new Color(tc.r,tc.g,tc.b,_alpha);
        GUI.Label(r,txt,s);
        if(hov&&Event.current.type==EventType.MouseDown&&Event.current.button==0){Event.current.Use();return true;}
        return false;
    }

    void DrawBorder(float x,float y,float w,float h,float t)
    {
        GUI.DrawTexture(new Rect(x,y,w,t),_wh);
        GUI.DrawTexture(new Rect(x,y+h-t,w,t),_wh);
        GUI.DrawTexture(new Rect(x,y,t,h),_wh);
        GUI.DrawTexture(new Rect(x+w-t,y,t,h),_wh);
    }

    GUIStyle Sty(int sz,FontStyle fs,Color col,TextAnchor a)
    { var s=new GUIStyle(); s.fontSize=sz; s.fontStyle=fs; s.normal.textColor=col; s.alignment=a; s.richText=true; return s; }
}
