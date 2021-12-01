using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class TetrisBlockController : UdonSharpBehaviour {
    private const int PositionSpawnX = 3;
    private const int PositionSpawnY = 2;
    private const int MaxY = 25;
    private const int MaxX = 10;

    //DAS & ARR
    //https://nestetrisjp.github.io/das-introduction/
    private const int MoveLongPressIntervalFrameCountMax = 3;
    private const int MoveLongPressStartCountMax = 15;
    private const int PieceTypeNone = 0;
    private const int PieceTypeI = 1;
    private const int PieceTypeO = 2;
    private const int PieceTypeS = 3;
    private const int PieceTypeZ = 4;
    private const int PieceTypeJ = 5;
    private const int PieceTypeL = 6;
    private const int PieceTypeT = 7;
    private const int PieceTypeGray = 8;
    private const int Angle0 = 0;
    private const int Angle90 = 1;
    private const int Angle180 = 2;
    private const int Angle270 = 3;
    private const int StateNotStarted = 0;
    private const int StateInitializeAndStart = 1;
    private const int StatePlaying = 2;
    private const int StatePacking = 3;
    private const int StatePiecePop = 4;
    private const int StateGameOver = 5;
    private const int StatePause = 6;
    private const int SpinStateNotTSpin = 0;
    private const int SpinStateMiniTSpin = 1;
    private const int SpinStateCorrectTSpin = 2;
    private const int StateArrowKeyNone = 0;
    private const int StateArrowKeyPress = 1;
    private const int StateArrowKeyLongPress = 2;

    public GameObject materials;
    public GameObject boardRoot;
    public GameObject boardPrev1Root;
    public GameObject boardPrev2Root;
    public GameObject previewNextPieces;
    public GameObject holdPiece;
    public TextMeshPro lineField;
    public TextMeshPro levelField;
    public TextMeshPro scoreField;
    public TextMeshPro localHighScoreField;
    public TextMeshPro globalHighScoreField;
    public TextMeshPro globalHighScoreNameField;
    public TextMeshPro startMessageField;
    public GameObject startLevelPanel;
    public TextMeshPro startLevelLeftField;
    public TextMeshPro startLevelField;
    public TextMeshPro startLevelRightField;
    public TextMeshPro scoreLogField;
    public GameObject previewControllerRoot;
    public GameObject pausePanel;

    //BGM
    public AudioSource bgmSource;
    public AudioSource bgmMasterSource;

    //SE
    public AudioSource moveSound;
    public AudioSource rotateSound;
    public AudioSource hardDropSound;
    public AudioSource deleteLineSound;
    public AudioSource deleteLineTetrisSound;
    public AudioSource holdSound;
    public AudioSource holdBlockingSound;
    public AudioSource gameOverSound;
    public AudioSource startSound;
    public AudioSource pauseSound;

    private Material[] _colorsCache;
    private GameObject[][] _boardBlocksGameObjectCache;
    private MeshRenderer[][] _boardBlocksMeshRendererCache;
    private MeshRenderer[][][] _previewNextPiecesMeshRendererCache;
    private MeshRenderer[][] _holdPiecesMeshRendererCache;
    private TetrisBlockPreviewController[] _previewControllers;
    private int[][][][] _piecePool;

    //Counter
    private double _autoMoveDownTime;
    private int _deleteDelayFrameCount;
    private int _moveLeftLongPressIntervalFrameCount;
    private int _moveRightLongPressIntervalFrameCount;
    private int _moveDownLongPressIntervalFrameCount;
    private int _gameOverDelayFrameCount;
    private int _globalHighScoreFieldUpdateFrameCount;
    private uint _renCount;
    private int _updatePreviewCount;
    private int _moveCountOnGround;

    //Input
    private bool _isLongPressingLeft;
    private bool _isLongPressingRight;

    //State
    private int _state;
    private int _tSpinState;
    private int _leftKeyState;
    private int _rightKeyState;
    private bool _isNeedRedraw;
    private bool _isUpdateGhost;
    private bool _hasAlreadyHoldCurrentPiece;
    private bool _back2Back;
    private bool _isMasterMode;
    private bool _irsLeftRotate;
    private bool _irsRightRotate;
    private bool _irsHold;

    //Score
    private int _currentDeleteLine;
    private long _currentScore;
    private long _localHighScore;
    private long _globalHighScoreCache;

    //Level
    private int _nextLevelCount;
    private int _currentLevel;
    private int _startLevel;

    //Field
    private int[][] _currentField;
    private int[][] _compositedFieldAll;
    private int[][][] _currentPiece;
    private int[][][][] _nextPiece;
    private int[][][] _holdPiece;

    [UdonSynced] private long _globalHighScoreSynced;
    [UdonSynced] private string _globalHighScoreNameSynced;

    private void Start() {
        //初期化
        InitMaterials();
        InitBoardBlocks();
        InitPreviewBlocks();
        InitHoldPiece();
        InitPreviewControllers();

        _currentField = CreateIntArrayMxN(MaxY, MaxX);
        _compositedFieldAll = _currentField;
        _nextPiece = new int[4][][][];

        //データのリセット
        _startLevel = 1;
        ResetStateAndCounter();

        //ボードを再描画
        RedrawBoardBlocks();
        RedrawNextPieces();
        RedrawHoldPiece();
        RedrawScore();

        //ステータスの設定
        _state = StateNotStarted;
    }

    private void InitMaterials() {
        _colorsCache = new Material[materials.transform.childCount];
        for (var i = 0; i < _colorsCache.Length; i++) {
            _colorsCache[i] = GetMaterial(i);
        }
    }

    /// <summary>
    /// マテリアルの取得
    /// Shader.FindあResourceの直接取得ができないのでGameObjectのRendererを経由して取得する
    /// </summary>
    /// <param name="index">マテリアルのインデックス</param>
    /// <returns>マテリアル</returns>
    private Material GetMaterial(int index) {
        return materials.transform.GetChild(index).GetComponent<MeshRenderer>().sharedMaterial;
    }

    private void InitBoardBlocks() {
        _boardBlocksGameObjectCache = new GameObject[MaxY][];
        _boardBlocksMeshRendererCache = new MeshRenderer[MaxY][];
        for (var i = 0; i < MaxY - 5; i++) {
            _boardBlocksGameObjectCache[i] = new GameObject[MaxX];
            _boardBlocksMeshRendererCache[i] = new MeshRenderer[MaxX];
            var rowObj = boardRoot.transform.GetChild(i);
            for (var j = 0; j < MaxX; j++) {
                var columnObj = rowObj.GetChild(j);
                _boardBlocksGameObjectCache[i][j] = columnObj.gameObject;
                _boardBlocksMeshRendererCache[i][j] = columnObj.gameObject.GetComponent<MeshRenderer>();
            }
        }
    }

    private void InitPreviewBlocks() {
        _previewNextPiecesMeshRendererCache = new MeshRenderer[4][][];
        for (var i = 0; i < 4; i++) {
            _previewNextPiecesMeshRendererCache[i] = new MeshRenderer[4][];
            var piece = previewNextPieces.transform.GetChild(i);
            for (var j = 0; j < 4; j++) {
                var rowObj = piece.transform.GetChild(j);
                _previewNextPiecesMeshRendererCache[i][j] = new MeshRenderer[4];
                for (var k = 0; k < 4; k++) {
                    var columnObj = rowObj.GetChild(k);
                    _previewNextPiecesMeshRendererCache[i][j][k] = columnObj.gameObject.GetComponent<MeshRenderer>();
                }
            }
        }
    }

    private void InitHoldPiece() {
        _holdPiecesMeshRendererCache = new MeshRenderer[4][];
        for (var i = 0; i < 4; i++) {
            _holdPiecesMeshRendererCache[i] = new MeshRenderer[4];
            var rowObj = holdPiece.transform.GetChild(i);
            for (var j = 0; j < 4; j++) {
                var columnObj = rowObj.GetChild(j);
                _holdPiecesMeshRendererCache[i][j] = columnObj.gameObject.GetComponent<MeshRenderer>();
            }
        }
    }

    private void InitPreviewControllers() {
        _previewControllers = new TetrisBlockPreviewController[8];
        for (var i = 0; i < 8; i++) {
            var preview = previewControllerRoot.transform.GetChild(i);
            var controller = preview.transform.GetChild(0).GetComponent<TetrisBlockPreviewController>();
            _previewControllers[i] = controller;
        }
    }

    private void ResetStateAndCounter() {
        //ピースのリセット
        InitPieces();

        //状態のリセット
        _autoMoveDownTime = 0.0;
        _deleteDelayFrameCount = 0;
        _moveLeftLongPressIntervalFrameCount = 0;
        _moveRightLongPressIntervalFrameCount = 0;
        _moveDownLongPressIntervalFrameCount = 0;
        _gameOverDelayFrameCount = 0;
        _moveCountOnGround = 0;
        _renCount = 0;
        _globalHighScoreFieldUpdateFrameCount = 0;
        _nextLevelCount = 10;
        _currentLevel = _startLevel;
        _tSpinState = SpinStateNotTSpin;
        _isNeedRedraw = false;
        _hasAlreadyHoldCurrentPiece = false;
        _back2Back = false;
        _currentDeleteLine = 0;
        _currentScore = 0;
    }

    private void InitPieces() {
        //最初のピースの生成
        //ゲーム開始時に「Z字形」「S字形」「四角形」のテトリミノが来ないようにする
        int type;
        do {
            //再抽選時に最初の7つで重複がこないようにリセットする
            ResetPiecePool();
            _currentPiece = CreateRandomPiece();
            type = GetPieceType(_currentPiece);
        } while (type == PieceTypeZ || type == PieceTypeS || type == PieceTypeO);

        _nextPiece[0] = CreateRandomPiece();
        _nextPiece[1] = CreateRandomPiece();
        _nextPiece[2] = CreateRandomPiece();
        _nextPiece[3] = CreateRandomPiece();

        _holdPiece = null;
    }

    private void Update() {
        switch (_state) {
            case StateNotStarted:
                StateNotStartedProcess();
                break;
            case StateInitializeAndStart:
                StateInitializeAndStartProcess();
                break;
            case StatePiecePop:
                StatePiecePopProcess();
                break;
            case StatePacking:
                StateDeleteAnimationProcess();
                break;
            case StatePlaying:
                StatePlayingProcess();
                break;
            case StateGameOver:
                StateGameOverProcess();
                break;
            case StatePause:
                StatePauseProcess();
                break;
        }

        //一定周期でグローバルハイスコアの更新
        if (_globalHighScoreFieldUpdateFrameCount > 60) {
            RedrawGlobalHighScore();
        }
    }

    private void StatePauseProcess() {
        if (Input.GetKeyDown(KeyCode.P)) {
            pauseSound.Play();
            pausePanel.SetActive(false);
            _state = StatePlaying;
        }
    }

    private void FixedUpdate() {
        switch (_state) {
            case StateNotStarted:
                break;
            case StateInitializeAndStart:
                break;
            case StatePiecePop:
                break;
            case StatePacking:
                //パッキングの遅延
                _deleteDelayFrameCount++;
                break;
            case StatePlaying:
                //移動のフレーム間隔カウント
                _moveLeftLongPressIntervalFrameCount++;
                _moveRightLongPressIntervalFrameCount++;
                _moveDownLongPressIntervalFrameCount++;
                break;
            case StateGameOver:
                break;
        }

        //一定周期でグローバルハイスコアの更新
        _globalHighScoreFieldUpdateFrameCount++;
    }

    private bool _isVMode;

    private void StateNotStartedProcess() {
        //レベル変更
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            if (_startLevel > 1) {
                if (_startLevel > 15) {
                    //ビネガーモードからの切り替え
                    if (_isVMode) {
                        _startLevel = 20;
                        startLevelField.text = $"M";
                        startLevelLeftField.enabled = true;
                        startLevelRightField.enabled = true;
                        //BGMの変更
                        bgmSource.Play();
                        bgmMasterSource.Stop();
                        _isMasterMode = true;
                        _isVMode = false;
                    }
                    //マスターモードからの切り替え
                    else if (_isMasterMode) {
                        _startLevel = 15;
                        startLevelField.text = $"{_startLevel}";
                        startLevelLeftField.enabled = true;
                        startLevelRightField.enabled = true;
                        _isMasterMode = false;
                    }
                } else {
                    _startLevel -= 1;
                    startLevelField.text = $"{_startLevel}";
                    startLevelLeftField.enabled = true;
                    startLevelRightField.enabled = true;
                }
            }
            if (_startLevel == 1) {
                startLevelLeftField.enabled = false;
            }
        }
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            if (_startLevel < 15) {
                _startLevel += 1;
                startLevelLeftField.enabled = true;
                startLevelRightField.enabled = true;
                startLevelField.text = $"{_startLevel}";
            }
            //レベル15からの右切り替えでマスターモードに移行
            if (_startLevel == 15) {
                _startLevel = 20;
                startLevelField.text = $"M";
                startLevelLeftField.enabled = true;
                startLevelRightField.enabled = true;
                _isMasterMode = true;
            }
            //マスターモードからの右切り替えでビネガーモードに移行
            else if (_isMasterMode) {
                _startLevel = 20;
                startLevelField.text = $"VM";
                startLevelLeftField.enabled = true;
                startLevelRightField.enabled = false;
                //BGMの変更
                bgmSource.Pause();
                bgmMasterSource.Play();
                _isMasterMode = false;
                _isVMode = true;
            }
        }
        //開始
        if (Input.GetKeyDown(KeyCode.U)) {
            _state = StateInitializeAndStart;
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player) {
        //同期表示のプレビュー更新
        switch (_state) {
            case StateNotStarted:
                break;
            case StateInitializeAndStart:
                break;
            case StatePiecePop:
            case StatePacking:
            case StatePlaying:
            case StateGameOver:
                UpdateSyncPreview(_compositedFieldAll);
                break;
            case StatePause:
                break;
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player) {
        //同期表示のプレビュー更新
        switch (_state) {
            case StateNotStarted:
                break;
            case StateInitializeAndStart:
                break;
            case StatePiecePop:
            case StatePacking:
            case StatePlaying:
            case StateGameOver:
                UpdateSyncPreview(_compositedFieldAll);
                break;
            case StatePause:
                break;
        }
    }

    private void StatePiecePopProcess() {
        //ピースを次に送る
        UpdatePiece();
        //キー入力の処理 (IRSを考慮)
        GetInputEventWithIrs();
        //行の削除がなければプレイ状態に戻る
        if (_state != StatePacking) {
            //ゴーストの位置更新
            UpdateGhostY();
            //再描画
            RedrawBoardBlocks();
            RedrawScore();
            //プレイ状態に戻る
            _state = StatePlaying;
        }
    }

    private void StateDeleteAnimationProcess() {
        //キー入力の処理 (IRSを考慮)
        GetInputEventWithIrs();

        if (_deleteDelayFrameCount > 24) {
            _deleteDelayFrameCount = 0;
            //削除した行を詰める
            PackLine();
            //ゴーストの位置更新
            UpdateGhostY();
            //再描画
            RedrawBoardBlocks();
            RedrawScore();
            //プレイ状態に戻る
            _state = StatePlaying;
        }
    }

    private void StatePlayingProcess() {
        //ポーズ受付
        if (Input.GetKeyDown(KeyCode.P)) {
            pauseSound.Play();
            pausePanel.SetActive(true);
            _state = StatePause;
            return;
        }
        
        //自動で下におくる処理のフレーム数
        _autoMoveDownTime += Time.deltaTime;

        //キー入力の処理
        GetInputEventWhenPlaying();
        if (_state == StatePiecePop) {
            return;
        }

        if (_isUpdateGhost) {
            //ゴーストの位置更新
            UpdateGhostY();
            _isUpdateGhost = false;
        }

        //一定フレームごとに下に移動
        var gravityTime = GetGravityTime();
        if (_autoMoveDownTime > gravityTime) {
            AutoMoveDown(gravityTime);
        }

        if (_isNeedRedraw) {
            _isNeedRedraw = false;
            //ボードを再描画
            RedrawBoardBlocks();
        }
    }

    private void AutoMoveDown(double gravityTime) {
        //レベルが高いと複数ブロック下がるようになる
        var count = (int) (_autoMoveDownTime / gravityTime);
        for (var i = 0; i < count; i++) {
            if (MoveDown()) {
                //移動したため再描画あり
                _isNeedRedraw = true;
                //自然落下したのでTスピンの判定を消す
                _tSpinState = SpinStateNotTSpin;
                //自然落下したのでロック遅延時間リセット
                ResetMoveDownAndLockDelayTime();
                //自然落下したので移動カウントリセット
                _moveCountOnGround = 0;
            } else {
                //下に移動できない場合

                //ロック遅延時間内の場合は固定スキップ
                if (_autoMoveDownTime < gravityTime + 0.5) {
                    //接地した状態で15回以上回転した場合は強制ロック
                    if (_moveCountOnGround < 15) {
                        return;
                    }
                }

                //ロック遅延時間外の場合は入れ替え、またはゲームオーバー
                //次のピースと重なるブロックがあるかでゲームオーバーチェック
                if (!CheckCollision(GetPieceData(_currentPiece), 0, 0)) {
                    //ゲームオーバー状態に遷移
                    gameOverSound.Play();
                    SetPieceGhostPosY(_currentPiece, PositionSpawnY);
                    _state = StateGameOver;
                } else {
                    //次のピースに入れ替え
                    _state = StatePiecePop;
                }

                return;
            }
        }
    }

    private double GetGravityTime() {
        //https://harddrop.com/wiki/Tetris_Worlds#Gravity
        //Time = (0.8-((Level-1)*0.007)) ^ (Level-1)
        return Pow(0.8 - (_currentLevel - 1) * 0.007, (_currentLevel - 1));
    }

    private double Pow(double x, int y) {
        var temp = x;
        for (var i = 0; i < y; i++) {
            temp *= x;
        }
        return temp;
    }

    private VRCPlayerApi[] PlayerIdSort(VRCPlayerApi[] players) {
        for (var i = 0; i < players.Length - 1; i++) {
            for (var j = players.Length - 1; j > i; j--) {
                if (players[j] != null && players[j - 1] != null) {
                    if (players[j].playerId < players[j - 1].playerId) {
                        var temp = players[j - 1];
                        players[j - 1] = players[j];
                        players[j] = temp;
                    }
                }
            }
        }
        return players;
    }

    private void UpdateSyncPreview(int[][] field) {
        var players = new VRCPlayerApi[20];
        var vrcPlayerApis = PlayerIdSort(VRCPlayerApi.GetPlayers(players));
        for (var i = 0; i < vrcPlayerApis.Length; i++) {
            var vrcPlayerApi = vrcPlayerApis[i];
            var localPlayer = Networking.LocalPlayer;
            if (localPlayer != null && vrcPlayerApi != null) {
                if (vrcPlayerApi.playerId == localPlayer.playerId) {
                    //同期表示のプレビュー更新
                    _previewControllers[i].UpdateField(SerializeField(field), _currentScore);
                }
            }
        }
    }

    private void StateInitializeAndStartProcess() {
        startSound.Play();
        StartMessageVisible(false);
        //新規ゲーム設定
        ArrayClearMxN(_currentField);
        ResetStateAndCounter();
        //ゴーストの位置更新
        UpdateGhostY();
        //ボードを再描画
        RedrawBoardBlocks();
        RedrawNextPieces();
        RedrawHoldPiece();
        RedrawScore();
        scoreLogField.text = "";
        _state = StatePlaying;
    }

    private void StateGameOverProcess() {
        if (_gameOverDelayFrameCount > 60) {
            //再スタート用メッセージを表示
            StartMessageVisible(true);
            //ポーズ状態に遷移
            _state = StateNotStarted;
            return;
        }
        //下からグレーアウト
        GameOver(_gameOverDelayFrameCount);
        _gameOverDelayFrameCount++;
        //ボードを再描画
        RedrawBoardBlocksWithoutPieceAndGhost();
    }

    private void GetInputEventWithIrs() {
        //先行入力、および入力の解除の処理
        //左回転
        if (Input.GetKeyDown(KeyCode.F)) {
            _irsLeftRotate = true;
            _irsRightRotate = false;
        }
        if (Input.GetKeyUp(KeyCode.F)) {
            _irsLeftRotate = false;
        }
        //右回転
        if (Input.GetKeyDown(KeyCode.G)) {
            _irsRightRotate = true;
            _irsLeftRotate = false;
        }
        if (Input.GetKeyUp(KeyCode.G)) {
            _irsRightRotate = false;
        }
        //左移動
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            _leftKeyState = StateArrowKeyPress;
            _isLongPressingLeft = true;
            //右キーの入力を解除
            _rightKeyState = StateArrowKeyNone;
            _isLongPressingRight = false;
        }
        if (Input.GetKey(KeyCode.LeftArrow)) {
            //押しっぱなしの場合
            if (_isLongPressingLeft) {
                _leftKeyState = StateArrowKeyLongPress;
            }
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow)) {
            _moveLeftLongPressIntervalFrameCount = 0;
            _isLongPressingLeft = false;
            _leftKeyState = StateArrowKeyNone;
        }
        //右移動
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            _rightKeyState = StateArrowKeyPress;
            _isLongPressingLeft = true;
            //左キーの入力を解除
            _leftKeyState = StateArrowKeyNone;
            _isLongPressingLeft = false;
        }
        if (Input.GetKey(KeyCode.RightArrow)) {
            //押しっぱなしの場合
            if (_isLongPressingRight) {
                _rightKeyState = StateArrowKeyLongPress;
            }
        }
        if (Input.GetKeyUp(KeyCode.RightArrow)) {
            _moveRightLongPressIntervalFrameCount = 0;
            _isLongPressingRight = false;
            _rightKeyState = StateArrowKeyNone;
        }
        //下移動の解除
        //消去アニメーション中にキーを離した場合にリセットする
        if (Input.GetKeyUp(KeyCode.DownArrow)) {
            _moveDownLongPressIntervalFrameCount = 0;
        }
        //ホールド
        if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.B)) {
            _irsHold = true;
        }
        if (Input.GetKeyUp(KeyCode.H) || Input.GetKeyUp(KeyCode.B)) {
            _irsHold = false;
        }
    }

    private void GetInputEventWhenPlaying() {
        //ホールド
        if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.B) || _irsHold) {
            _irsHold = false;

            if (HoldPiece()) {
                //ホールド有効
                holdSound.Play();
                _isNeedRedraw = true;
                //落下カウントリセット
                ResetMoveDownAndLockDelayTime();
                //移動カウントリセット
                _moveCountOnGround = 0;
            } else {
                //ホールド無効
                holdBlockingSound.Play();
            }
        }
        //左回転
        if (Input.GetKeyDown(KeyCode.F) || _irsLeftRotate) {
            _irsLeftRotate = false;

            if (RotateLeft()) {
                rotateSound.Play();
                _isNeedRedraw = true;
                //回転が適用できた場合は落下カウントに猶予を追加
                ResetMoveDownAndLockDelayTime();
                //移動カウント更新
                _moveCountOnGround++;
            }
        }
        //右回転
        if (Input.GetKeyDown(KeyCode.G) || _irsRightRotate) {
            _irsRightRotate = false;

            if (RotateRight()) {
                rotateSound.Play();
                _isNeedRedraw = true;
                //回転が適用できた場合は落下カウントに猶予を追加
                ResetMoveDownAndLockDelayTime();
                //移動カウント更新
                _moveCountOnGround++;
            }
        }
        //ハードドロップ
        if (Input.GetKeyDown(KeyCode.UpArrow)) {
            hardDropSound.Play();
            HardDrop();
            _isNeedRedraw = true;
        }
        //左移動
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            _leftKeyState = StateArrowKeyPress;
            //右キーの入力を解除
            _rightKeyState = StateArrowKeyNone;
            _isLongPressingRight = false;
        }
        if (Input.GetKey(KeyCode.LeftArrow)) {
            //押しっぱなしの場合
            if (_isLongPressingLeft) {
                _leftKeyState = StateArrowKeyLongPress;
            }
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow)) {
            _moveLeftLongPressIntervalFrameCount = 0;
            _isLongPressingLeft = false;
            _leftKeyState = StateArrowKeyNone;
        }
        switch (_leftKeyState) {
            case StateArrowKeyNone:
                break;
            case StateArrowKeyPress:
                if (MoveLeft()) {
                    moveSound.Play();
                    _isNeedRedraw = true;
                    //移動カウント更新
                    _moveCountOnGround++;
                    //移動が適用できた場合は落下カウントに猶予を追加
                    ResetMoveDownAndLockDelayTime();
                    _moveLeftLongPressIntervalFrameCount = -MoveLongPressStartCountMax;
                    _isLongPressingLeft = true;
                }
                break;
            case StateArrowKeyLongPress:
                if (_moveLeftLongPressIntervalFrameCount > MoveLongPressIntervalFrameCountMax) {
                    if (MoveLeft()) {
                        moveSound.Play();
                        _isNeedRedraw = true;
                        //移動カウント更新
                        _moveCountOnGround++;
                        //移動が適用できた場合は落下カウントに猶予を追加
                        ResetMoveDownAndLockDelayTime();
                        _moveLeftLongPressIntervalFrameCount = 0;
                    }
                }
                break;
        }
        //右移動
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            _rightKeyState = StateArrowKeyPress;
            //左キーの入力を解除
            _leftKeyState = StateArrowKeyNone;
            _isLongPressingLeft = false;
        }
        if (Input.GetKey(KeyCode.RightArrow)) {
            //押しっぱなしの場合
            if (_isLongPressingRight) {
                _rightKeyState = StateArrowKeyLongPress;
            }
        }
        if (Input.GetKeyUp(KeyCode.RightArrow)) {
            _moveRightLongPressIntervalFrameCount = 0;
            _isLongPressingRight = false;
            _rightKeyState = StateArrowKeyNone;
        }
        switch (_rightKeyState) {
            case StateArrowKeyNone:
                break;
            case StateArrowKeyPress:
                if (MoveRight()) {
                    moveSound.Play();
                    _isNeedRedraw = true;
                    //移動カウント更新
                    _moveCountOnGround++;
                    //移動が適用できた場合は落下カウントに猶予を追加
                    ResetMoveDownAndLockDelayTime();
                    _moveRightLongPressIntervalFrameCount = -MoveLongPressStartCountMax;
                    _isLongPressingRight = true;
                }
                break;
            case StateArrowKeyLongPress:
                if (_moveRightLongPressIntervalFrameCount > MoveLongPressIntervalFrameCountMax) {
                    if (MoveRight()) {
                        moveSound.Play();
                        _isNeedRedraw = true;
                        //移動カウント更新
                        _moveCountOnGround++;
                        //移動が適用できた場合は落下カウントに猶予を追加
                        ResetMoveDownAndLockDelayTime();
                        _moveRightLongPressIntervalFrameCount = 0;
                    }
                }
                break;
        }
        //下移動(Soft Drop)
        if (Input.GetKey(KeyCode.DownArrow)) {
            if (_moveDownLongPressIntervalFrameCount > MoveLongPressIntervalFrameCountMax) {
                if (MoveDown()) {
                    moveSound.Play();
                    _isNeedRedraw = true;
                    //落下したらTスピンの判定を消す
                    _tSpinState = SpinStateNotTSpin;
                    _moveDownLongPressIntervalFrameCount = 0;
                    //移動が適用できた場合は落下カウントに猶予を追加
                    ResetMoveDownAndLockDelayTime();
                    //落下したので移動カウントリセット
                    _moveCountOnGround = 0;
                }
            }
        }
        if (Input.GetKeyUp(KeyCode.DownArrow)) {
            _moveDownLongPressIntervalFrameCount = 0;
        }
    }

    private void RedrawBoardBlocksWithoutPieceAndGhost() {
        //ピースとゴーストは描画しない
        _compositedFieldAll = _currentField;
        for (var i = 0; i < MaxY - 5; i++) {
            for (var j = 0; j < MaxX; j++) {
                var blockType = _compositedFieldAll[i + 5][j];
                if (blockType == PieceTypeNone) {
                    _boardBlocksGameObjectCache[i][j].SetActive(false);
                } else {
                    SetMaterial(_boardBlocksMeshRendererCache[i][j], blockType);
                    _boardBlocksGameObjectCache[i][j].SetActive(true);
                }
            }
        }

        //同期表示のプレビュー更新
        UpdateSyncPreview(_compositedFieldAll);
    }

    private void RedrawBoardBlocks() {
        //ゴーストの合成 (レベル16以上ではゴーストは非表示)
        var compositedField = _currentLevel < 16 ? CompositeGhostToField() : _currentField;
        //現在のピースを表示用にフィールドに合成
        _compositedFieldAll = CompositePieceToField(compositedField);
        for (var i = 0; i < MaxY - 5; i++) {
            for (var j = 0; j < MaxX; j++) {
                var blockType = _compositedFieldAll[i + 5][j];
                if (blockType == PieceTypeNone) {
                    _boardBlocksGameObjectCache[i][j].SetActive(false);
                } else {
                    SetMaterial(_boardBlocksMeshRendererCache[i][j], blockType);
                    _boardBlocksGameObjectCache[i][j].SetActive(true);
                }
            }
        }

        //同期表示のプレビュー更新
        UpdateSyncPreview(_compositedFieldAll);
    }

    private void RedrawNextPieces() {
        for (var i = 0; i < 4; i++) {
            var preview = GetPiecePreview(_nextPiece[i]);
            for (var j = 0; j < 4; j++) {
                for (var k = 0; k < 4; k++) {
                    SetMaterial(_previewNextPiecesMeshRendererCache[i][j][k], preview[j][k]);
                }
            }
        }
    }

    private void RedrawHoldPiece() {
        if (_holdPiece != null) {
            var preview = GetPiecePreview(_holdPiece);
            for (var j = 0; j < 4; j++) {
                for (var k = 0; k < 4; k++) {
                    SetMaterial(_holdPiecesMeshRendererCache[j][k], preview[j][k]);
                }
            }
        } else {
            for (var j = 0; j < 4; j++) {
                for (var k = 0; k < 4; k++) {
                    SetMaterial(_holdPiecesMeshRendererCache[j][k], 0);
                }
            }
        }
    }

    private void RedrawScore() {
        //ハイスコアの記録
        if (_currentScore > _localHighScore) {
            _localHighScore = _currentScore;
            localHighScoreField.text = $"{_localHighScore}";
        }
        lineField.text = $"{_currentDeleteLine}";
        levelField.text = $"{_currentLevel}";
        scoreField.text = $"{_currentScore}";
    }

    private void RedrawGlobalHighScore() {
        //自分がハイスコアを更新
        if (_currentScore > _globalHighScoreSynced) {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            if (Networking.IsOwner(Networking.LocalPlayer, gameObject)) {
                _globalHighScoreSynced = _currentScore;
                _globalHighScoreFieldUpdateFrameCount = 0;
                globalHighScoreField.text = $"{_globalHighScoreSynced}";

                var localPlayer = Networking.LocalPlayer;
                if (localPlayer != null) {
                    _globalHighScoreNameSynced = localPlayer.displayName;
                    globalHighScoreNameField.text = _globalHighScoreNameSynced;
                }
                
                RequestSerialization();
            }
        }
        //他プレイヤーがハイスコアを更新
        if (_globalHighScoreSynced > _globalHighScoreCache) {
            _globalHighScoreCache = _globalHighScoreSynced;
            _globalHighScoreFieldUpdateFrameCount = 0;
            globalHighScoreField.text = $"{_globalHighScoreSynced}";
            globalHighScoreNameField.text = _globalHighScoreNameSynced;
        }
    }

    private void StartMessageVisible(bool enable) {
        if (enable) {
            startMessageField.enabled = true;
            startLevelPanel.SetActive(true);
        } else {
            startMessageField.enabled = false;
            startLevelPanel.SetActive(false);
        }
    }

    private void SetMaterial(MeshRenderer render, int blockType) {
        if (render.sharedMaterial != _colorsCache[blockType]) {
            render.sharedMaterial = _colorsCache[blockType];
        }
    }

    private int[][] CompositePieceToField(int[][] field) {
        var compositedField = CreateIntArrayMxN(MaxY, MaxX);

        //現在のフィールドの状態をコピー
        ArrayCopyMxN(field, compositedField);

        //表示用フィールドにピースを合成
        var data = GetPieceData(_currentPiece);
        var size = GetPieceSize(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var y = posY + i;
                var x = posX + j;
                if (x >= 0 && x < MaxX && y >= 0 && y < MaxY) {
                    compositedField[y][x] = block;
                }
            }
        }

        return compositedField;
    }

    private int[][] CompositeGhostToField() {
        var compositedField = CreateIntArrayMxN(MaxY, MaxX);

        //現在のフィールドの状態をコピー
        ArrayCopyMxN(_currentField, compositedField);

        //表示用フィールドにピースを合成
        var data = GetPieceData(_currentPiece);
        var size = GetPieceSize(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPieceGhostPosY(_currentPiece);
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var y = posY + i;
                var x = posX + j;
                if (x >= 0 && x < MaxX && y >= 0 && y < MaxY) {
                    compositedField[y][x] = block + 8;
                }
            }
        }

        return compositedField;
    }

    private void FixedCurrentField() {
        //ピースが接地した場合
        //現在のピースの状態を固定
        var compositedField = CompositePieceToField(_currentField);
        ArrayCopyMxN(compositedField, _currentField);
    }

    private void GameOver(int count) {
        //下から順番にグレーアウト
        var y = MaxY - count - 1;
        if (y < 0) return;

        for (var j = 0; j < MaxX; j++) {
            if (_currentField[y][j] != PieceTypeNone) {
                _currentField[y][j] = PieceTypeGray;
            }
        }
    }

    private void UpdatePiece() {
        //ピースの位置を確定し、現在の状態を固定
        FixedCurrentField();

        //削除処理
        var filledLine = DeleteFilledLine();
        if (filledLine > 0) {
            //削除時のSEの再生
            if (filledLine >= 4) {
                deleteLineTetrisSound.Play();
            } else if (filledLine == 3) {
                if (_tSpinState == SpinStateCorrectTSpin || _tSpinState == SpinStateMiniTSpin) {
                    deleteLineTetrisSound.Play();
                } else {
                    deleteLineSound.Play();
                }
            } else {
                if (_tSpinState == SpinStateCorrectTSpin) {
                    deleteLineTetrisSound.Play();
                } else {
                    deleteLineSound.Play();
                }
            }
            //削除ブロックがあるので再描画
            RedrawBoardBlocksWithoutPieceAndGhost();
            //アニメーション&パッキング処理
            _state = StatePacking;
        }

        //削除したらロック遅延時間リセット
        ResetMoveDownAndLockDelayTime();

        //ピースを次に送る
        PopNextPiece();

        //ホールド規制を解除
        _hasAlreadyHoldCurrentPiece = false;
    }

    private void PopNextPiece() {
        _currentPiece = _nextPiece[0];
        _nextPiece[0] = _nextPiece[1];
        _nextPiece[1] = _nextPiece[2];
        _nextPiece[2] = _nextPiece[3];
        _nextPiece[3] = CreateRandomPiece();

        RedrawNextPieces();
    }

    private int DeleteFilledLine() {
        var filledLineNumber = 0;
        for (var i = 0; i < MaxY; i++) {
            //ラインが埋まっているかチェック
            var isFilled = true;
            for (var j = 0; j < MaxX; j++) {
                if (_currentField[i][j] != 0) {
                    continue;
                }
                isFilled = false;
                break;
            }

            //ラインが埋まっている場合ラインをクリア(発光マテリアルに変更)
            if (isFilled) {
                for (var j = 0; j < MaxX; j++) {
                    _currentField[i][j] += 15;
                }
                _currentDeleteLine++;
                filledLineNumber++;
                _nextLevelCount--;
                //レベルの更新
                UpdateLevel();
            }
        }

        //スコアとログの更新
        UpdateScoreAndLog(filledLineNumber);

        return filledLineNumber;
    }

    private void UpdateLevel() {
        if (_nextLevelCount <= 0) {
            _currentLevel++;
            _nextLevelCount = _currentLevel * 10;
        }
    }

    private void UpdateScoreAndLog(int filledLineNumber) {
        var temp = "";
        switch (filledLineNumber) {
            case 0:
                _renCount = 0;
                break;
            case 1:
                switch (_tSpinState) {
                    case SpinStateNotTSpin:
                        var addScoreNt = UpdateScore(100);
                        temp = $"Single +{addScoreNt}";
                        _back2Back = false;
                        break;
                    case SpinStateMiniTSpin:
                        var addScoreMt = UpdateScore(200);
                        temp = $"Mini T-Spin Single +{addScoreMt}";
                        _back2Back = true;
                        break;
                    case SpinStateCorrectTSpin:
                        if (_back2Back) {
                            var addScore = UpdateScore(1200);
                            temp = $"B2B T-Spin Single +{addScore}";
                        } else {
                            var addScore = UpdateScore(800);
                            temp = $"T-Spin Single +{addScore}";
                        }
                        _back2Back = true;
                        break;
                }
                _renCount++;
                if (_renCount > 1) {
                    var renScore = (_renCount - 1) * (_renCount > 10 ? 100 : 50);
                    var addScore = UpdateScore(renScore);
                    temp += $"<br>Ren x{_renCount - 1} +{addScore}";
                }
                scoreLogField.text = temp;
                break;
            case 2:
                switch (_tSpinState) {
                    case SpinStateNotTSpin:
                        var addScoreNt = UpdateScore(300);
                        temp = $"Double +{addScoreNt}";
                        _back2Back = false;
                        break;
                    case SpinStateMiniTSpin:
                        if (_back2Back) {
                            var addScore = UpdateScore(600);
                            temp = $"B2B Mini T-Spin Double +{addScore}";
                        } else {
                            var addScore = UpdateScore(400);
                            temp = $"Mini T-Spin Double +{addScore}";
                        }
                        _back2Back = true;
                        break;
                    case SpinStateCorrectTSpin:
                        if (_back2Back) {
                            var addScore = UpdateScore(1800);
                            temp = $"B2B T-Spin Double +{addScore}";
                        } else {
                            var addScore = UpdateScore(1200);
                            temp = $"T-Spin Double +{addScore}";
                        }
                        _back2Back = true;
                        break;
                }
                _renCount++;
                if (_renCount > 1) {
                    var renScore = (_renCount - 1) * (_renCount > 10 ? 100 : 50);
                    var addScore = UpdateScore(renScore);
                    temp += $"<br>Ren x{_renCount - 1} +{addScore}";
                }
                scoreLogField.text = temp;
                break;
            case 3:
                switch (_tSpinState) {
                    case SpinStateNotTSpin:
                        var addScoreNt = UpdateScore(500);
                        temp = $"Triple +{addScoreNt}";
                        _back2Back = false;
                        break;
                    case SpinStateMiniTSpin:
                    case SpinStateCorrectTSpin:
                        if (_back2Back) {
                            var addScore = UpdateScore(2400);
                            temp = $"B2B T-Spin Triple +{addScore}";
                        } else {
                            var addScore = UpdateScore(1600);
                            temp = $"T-Spin Triple +{addScore}";
                        }
                        _back2Back = true;
                        break;
                }
                _renCount++;
                if (_renCount > 1) {
                    var renScore = (_renCount - 1) * (_renCount > 10 ? 100 : 50);
                    var addScore = UpdateScore(renScore);
                    temp += $"<br>Ren x{_renCount - 1} +{addScore}";
                }
                scoreLogField.text = temp;
                break;
            case 4:
                if (_back2Back) {
                    var addScore = UpdateScore(1200);
                    temp = $"B2B Tetris +{addScore}";
                } else {
                    var addScore = UpdateScore(800);
                    temp = $"Tetris +{addScore}";
                }
                _renCount++;
                if (_renCount > 1) {
                    var renScore = (_renCount - 1) * (_renCount > 10 ? 100 : 50);
                    var addScore = UpdateScore(renScore);
                    temp += $"<br>Ren x{_renCount - 1} +{addScore}";
                }
                scoreLogField.text = temp;
                _back2Back = true;
                break;
        }
    }

    private long UpdateScore(long score) {
        var addScore = score * _currentLevel;
        _currentScore += addScore;
        return addScore;
    }

    private void PackLine() {
        var packedField = CreateIntArrayMxN(MaxY, MaxX);
        var y = MaxY - 1;
        for (var i = MaxY - 1; i >= 0; i--) {
            //ラインにブロックが存在するかチェック
            var isExistBlock = false;
            for (var j = 0; j < MaxX; j++) {
                if (_currentField[i][j] > 0 && _currentField[i][j] < 8) {
                    isExistBlock = true;
                    break;
                }
            }

            //ブロックが存在する場合ラインを詰めてコピー
            if (isExistBlock) {
                for (var j = 0; j < MaxX; j++) {
                    if (_currentField[i][j] > 0 && _currentField[i][j] < 8) {
                        packedField[y][j] = _currentField[i][j];
                    }
                }
                y--;
            }
        }
        // パッキングを適用
        ArrayCopyMxN(packedField, _currentField);
    }

    private void ResetMoveDownAndLockDelayTime() {
        //自然落下とロック遅延時間をリセット
        _autoMoveDownTime = 0;
    }

    private bool RotateLeft() {
        var data = GetPieceData(_currentPiece);
        var type = GetPieceType(_currentPiece);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        //タイプOは無条件でtrue
        if (type == PieceTypeO) {
            return true;
        }

        var rotatedData = GetRotateLeftData(data);
        var currentAngle = GetPieceAngle(_currentPiece);

        //https://tetris.wiki/Super_Rotation_System
        var wallKickData = CreateIntArrayMxN(5, 2);
        int nextAngle;
        if (type == PieceTypeI) {
            switch (currentAngle) {
                case Angle0: //0->L
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {+2, 0}, {-1, +2}, {+2, -1}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 2;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = -1;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = 2;
                    wallKickData[4][1] = -1;
                    nextAngle = Angle270;
                    break;
                case Angle90: //R->0
                    // wallKickData = new[,] {{0, 0}, {+2, 0}, {-1, 0}, {+2, +1}, {-1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 2;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = 2;
                    wallKickData[3][1] = 1;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle0;
                    break;
                case Angle180: //2->R
                    // wallKickData = new[,] {{0, 0}, {+1, 0}, {-2, 0}, {+1, -2}, {-2, +1}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -2;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = 1;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = -2;
                    wallKickData[4][1] = 1;
                    nextAngle = Angle90;
                    break;
                default: //L->2
                    // wallKickData = new[,] {{0, 0}, {-2, 0}, {+1, 0}, {-2, -1}, {+1, +2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -2;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = -2;
                    wallKickData[3][1] = -1;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle180;
                    break;
            }
        } else {
            switch (currentAngle) {
                case Angle0: //0->L
                    // wallKickData = new[,] {{0, 0}, {1, 0}, {1, 1}, {0, -2}, {1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = 1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle270;
                    break;
                case Angle90: //R->0
                    // wallKickData = new[,] {{0, 0}, {1, 0}, {1, -1}, {0, 2}, {1, 2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = -1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle0;
                    break;
                case Angle180: //2->R
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {-1, 1}, {0, -2}, {-1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = 1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle90;
                    break;
                default: //L->2
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {-1, -1}, {0, 2}, {-1, 2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = -1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle180;
                    break;
            }
        }

        for (var i = 0; i < 5; i++) {
            var offsetX = wallKickData[i][0];
            var offsetY = -wallKickData[i][1];
            if (CheckCollision(rotatedData, offsetX, offsetY)) {
                // 回転を適用
                SetPieceData(_currentPiece, rotatedData);
                SetPiecePosX(_currentPiece, posX + offsetX);
                SetPiecePosY(_currentPiece, posY + offsetY);
                SetPieceAngle(_currentPiece, nextAngle);
                //Tスピンの判定
                CheckTSpin();
                //ゴーストの位置更新
                _isUpdateGhost = true;
                return true;
            }
        }

        return false;
    }

    private bool RotateRight() {
        var data = GetPieceData(_currentPiece);
        var type = GetPieceType(_currentPiece);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        //タイプOは無条件でtrue
        if (type == PieceTypeO) {
            return true;
        }

        var rotatedData = GetRotateRightData(data);
        var currentAngle = GetPieceAngle(_currentPiece);

        //https://tetris.wiki/Super_Rotation_System
        var wallKickData = CreateIntArrayMxN(5, 2);
        int nextAngle;
        if (type == PieceTypeI) {
            switch (currentAngle) {
                case Angle0: //0->R
                    // wallKickData = new[,] {{0, 0}, {-2, 0}, {+1, 0}, {-2, -1}, {+1, +2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -2;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = -2;
                    wallKickData[3][1] = -1;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle90;
                    break;
                case Angle90: //R->2
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {+2, 0}, {-1, +2}, {+2, -1}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 2;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = -1;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = 2;
                    wallKickData[4][1] = -1;
                    nextAngle = Angle180;
                    break;
                case Angle180: //2->L
                    // wallKickData = new[,] {{0, 0}, {+2, 0}, {-1, 0}, {+2, +1}, {-1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 2;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = 2;
                    wallKickData[3][1] = 1;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle270;
                    break;
                default: //L->0
                    // wallKickData = new[,] {{0, 0}, {+1, 0}, {-2, 0}, {+1, -2}, {-2, +1}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -2;
                    wallKickData[2][1] = 0;
                    wallKickData[3][0] = 1;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = -2;
                    wallKickData[4][1] = 1;
                    nextAngle = Angle0;
                    break;
            }
        } else {
            switch (currentAngle) {
                case Angle0: //0->R
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {-1, 1}, {0, -2}, {-1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = 1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle90;
                    break;
                case Angle90: //R->2
                    // wallKickData = new[,] {{0, 0}, {1, 0}, {1, -1}, {0, 2}, {1, 2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = -1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle180;
                    break;
                case Angle180: //2->L
                    // wallKickData = new[,] {{0, 0}, {1, 0}, {1, 1}, {0, -2}, {1, -2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = 1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = 1;
                    wallKickData[2][1] = 1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = -2;
                    wallKickData[4][0] = 1;
                    wallKickData[4][1] = -2;
                    nextAngle = Angle270;
                    break;
                default: //L->0
                    // wallKickData = new[,] {{0, 0}, {-1, 0}, {-1, -1}, {0, 2}, {-1, 2}};
                    wallKickData[0][0] = 0;
                    wallKickData[0][1] = 0;
                    wallKickData[1][0] = -1;
                    wallKickData[1][1] = 0;
                    wallKickData[2][0] = -1;
                    wallKickData[2][1] = -1;
                    wallKickData[3][0] = 0;
                    wallKickData[3][1] = 2;
                    wallKickData[4][0] = -1;
                    wallKickData[4][1] = 2;
                    nextAngle = Angle0;
                    break;
            }
        }

        for (var i = 0; i < 5; i++) {
            var offsetX = wallKickData[i][0];
            var offsetY = -wallKickData[i][1];
            if (CheckCollision(rotatedData, offsetX, offsetY)) {
                // 回転を適用
                SetPieceData(_currentPiece, rotatedData);
                SetPiecePosX(_currentPiece, posX + offsetX);
                SetPiecePosY(_currentPiece, posY + offsetY);
                SetPieceAngle(_currentPiece, nextAngle);
                //Tスピンの判定
                CheckTSpin();
                //ゴーストの位置更新
                _isUpdateGhost = true;
                return true;
            }
        }

        return false;
    }


    private bool CheckCollision(int[][] data, int offsetX, int offsetY) {
        var size = GetPieceSize(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        //ピースがブロックと衝突するか
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var x = posX + j + offsetX;
                var y = posY + i + offsetY;
                if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                    _currentField[y][x] != 0) {
                    // 衝突する場合
                    return false;
                }
            }
        }

        return true;
    }

    private void CheckTSpin() {
        var type = GetPieceType(_currentPiece);
        if (type == PieceTypeT) {
            var posY = GetPiecePosY(_currentPiece);
            var posX = GetPiecePosX(_currentPiece);
            var angle = GetPieceAngle(_currentPiece);

            //四隅がブロックと衝突するか
            var count = 0;
            var y = posY;
            var x = posX;
            if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                _currentField[y][x] != 0) {
                count++;
            }
            y = posY;
            x = posX + 2;
            if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                _currentField[y][x] != 0) {
                count++;
            }
            y = posY + 2;
            x = posX;
            if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                _currentField[y][x] != 0) {
                count++;
            }
            y = posY + 2;
            x = posX + 2;
            if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                _currentField[y][x] != 0) {
                count++;
            }

            if (count >= 3) {
                _tSpinState = SpinStateCorrectTSpin;
                switch (angle) {
                    case Angle0:
                        y = posY + 2;
                        x = posX + 1;
                        if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                            _currentField[y][x] != 0) {
                            _tSpinState = SpinStateMiniTSpin;
                        }
                        return;
                    case Angle90:
                        y = posY + 1;
                        x = posX;
                        if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                            _currentField[y][x] != 0) {
                            _tSpinState = SpinStateMiniTSpin;
                        }
                        return;
                    case Angle180:
                        y = posY;
                        x = posX + 1;
                        if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                            _currentField[y][x] != 0) {
                            _tSpinState = SpinStateMiniTSpin;
                        }
                        return;
                    case Angle270:
                        y = posY + 1;
                        x = posX + 2;
                        if (x < 0 || x >= MaxX || y < 0 || y >= MaxY ||
                            _currentField[y][x] != 0) {
                            _tSpinState = SpinStateMiniTSpin;
                        }
                        return;
                }
            } else {
                _tSpinState = SpinStateNotTSpin;
            }
        }
    }

    private void HardDrop() {
        while (MoveDown()) {
            //落下したらTスピンの判定を消す
            _tSpinState = SpinStateNotTSpin;
            //ロック遅延をリセット
            ResetMoveDownAndLockDelayTime();
            //落下したので移動カウントリセット
            _moveCountOnGround = 0;
        }
        //ピースを次に送る
        _state = StatePiecePop;
        //ゴーストの位置更新
        _isUpdateGhost = true;
    }

    private bool MoveLeft() {
        var data = GetPieceData(_currentPiece);
        var left = GetLeft(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        // はみ出し判定
        if (posX + left <= 0) {
            return false;
        }

        // 左にブロックがあるか
        var size = GetPieceSize(data);
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var x = posX + j - 1;
                var y = posY + i;
                if (x >= 0 && x < MaxX && y >= 0 && y < MaxY &&
                    _currentField[y][x] != PieceTypeNone) {
                    return false;
                }
            }
        }

        SetPiecePosX(_currentPiece, posX - 1);

        //ゴーストの位置更新
        _isUpdateGhost = true;

        return true;
    }

    private bool MoveRight() {
        var data = GetPieceData(_currentPiece);
        var right = GetRight(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        // はみ出し判定
        if (posX + right >= MaxX - 1) {
            return false;
        }

        // 右にブロックがあるか
        var size = GetPieceSize(data);
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var x = posX + j + 1;
                var y = posY + i;
                if (x >= 0 && x < MaxX && y >= 0 && y < MaxY &&
                    _currentField[y][x] != PieceTypeNone) {
                    return false;
                }
            }
        }

        SetPiecePosX(_currentPiece, posX + 1);

        //ゴーストの位置更新
        _isUpdateGhost = true;

        return true;
    }

    private bool MoveDown() {
        var data = GetPieceData(_currentPiece);
        var bottom = GetBottom(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        // はみ出し判定
        if (posY + bottom >= MaxY - 1) {
            return false;
        }

        // 下にブロックがあるか
        var size = GetPieceSize(data);
        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                var block = data[i][j];
                if (block == PieceTypeNone) continue;

                var x = posX + j;
                var y = posY + i + 1;
                if (x >= 0 && x < MaxX && y >= 0 && y < MaxY &&
                    _currentField[y][x] != PieceTypeNone) {
                    return false;
                }
            }
        }

        SetPiecePosY(_currentPiece, posY + 1);

        //ゴーストの位置更新
        _isUpdateGhost = true;

        return true;
    }

    private void UpdateGhostY() {
        //16レベル以上ではゴーストが非表示になるのでスキップ
        if (_currentLevel > 15) {
            return;
        }

        var data = GetPieceData(_currentPiece);
        var bottom = GetBottom(data);
        var posX = GetPiecePosX(_currentPiece);
        var posY = GetPiecePosY(_currentPiece);

        //ゴースト表示位置
        var size = GetPieceSize(data);
        for (var gy = posY; gy < MaxY; gy++) {
            //はみ出し判定
            if (gy + bottom >= MaxY - 1) {
                SetPieceGhostPosY(_currentPiece, gy);
                return;
            }

            for (var i = 0; i < size; i++) {
                for (var j = 0; j < size; j++) {
                    var block = data[i][j];
                    if (block == PieceTypeNone) continue;

                    var x = posX + j;
                    var y = gy + i + 1;
                    if (x >= 0 && x < MaxX && y >= 0 && y < MaxY &&
                        _currentField[y][x] != PieceTypeNone) {
                        SetPieceGhostPosY(_currentPiece, gy);
                        return;
                    }
                }
            }
        }
    }

    private bool HoldPiece() {
        //初回
        if (_holdPiece == null) {
            //ホールドに格納
            _holdPiece = _currentPiece;
            PopNextPiece();

            //ゴーストの位置更新
            _isUpdateGhost = true;

            RedrawHoldPiece();
            _hasAlreadyHoldCurrentPiece = true;
            return true;
        }

        //初回以降
        //ホールドは１ピースごとに１回のみ
        if (_hasAlreadyHoldCurrentPiece) {
            return false;
        }

        //現在のピースは同タイプでリセットする(プールからは取り出さず新規)
        var temp = CreatePieceFromIndex(GetPieceType(_holdPiece));

        //ホールドとピースの入れ替え
        _holdPiece = _currentPiece;
        _currentPiece = temp;

        //ゴーストの位置更新
        _isUpdateGhost = true;

        RedrawHoldPiece();

        _hasAlreadyHoldCurrentPiece = true;

        return true;
    }

    private int GetBottom(int[][] data) {
        var size = GetPieceSize(data);
        for (var i = size - 1; i >= 0; i--) {
            for (var j = size - 1; j >= 0; j--) {
                if (data[i][j] != PieceTypeNone) {
                    return i;
                }
            }
        }
        return -100; //Invalid
    }

    private int GetLeft(int[][] data) {
        var size = GetPieceSize(data);
        for (var j = 0; j < size; j++) {
            for (var i = 0; i < size; i++) {
                if (data[i][j] != PieceTypeNone) {
                    return j;
                }
            }
        }
        return -100; //Invalid
    }

    private int GetRight(int[][] data) {
        var size = GetPieceSize(data);
        for (var j = size - 1; j >= 0; j--) {
            for (var i = size - 1; i >= 0; i--) {
                if (data[i][j] != PieceTypeNone) {
                    return j;
                }
            }
        }
        return -100; //Invalid
    }

    private int[][] GetRotateRightData(int[][] data) {
        var size = GetPieceSize(data);
        var result = CreateIntArrayMxN(size, size);

        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                result[j][size - i - 1] = data[i][j];
            }
        }

        return result;
    }

    private int[][] GetRotateLeftData(int[][] data) {
        var size = GetPieceSize(data);
        var result = CreateIntArrayMxN(size, size);

        for (var i = 0; i < size; i++) {
            for (var j = 0; j < size; j++) {
                result[size - j - 1][i] = data[i][j];
            }
        }

        return result;
    }

    /// <summary>
    /// PiecePoolの再生成
    /// </summary>
    private void ResetPiecePool() {
        _piecePool = new int[7][][][];
        for (var i = 0; i < 7; i++) {
            _piecePool[i] = CreatePieceFromIndex(i + 1);
        }
    }

    private int[][][] getOnePiece() {
        var piecePoolNextLength = _piecePool.Length - 1;
        var nextPiecePool = new int[piecePoolNextLength][][][];
        var nextIndex = UnityEngine.Random.Range(0, _piecePool.Length - 1);
        var nextPiece = _piecePool[nextIndex];
        var j = 0;
        for (var i = 0; i < _piecePool.Length; i++) {
            if (nextIndex != i) {
                nextPiecePool[j] = _piecePool[i];
                j++;
            }
        }
        _piecePool = nextPiecePool;
        return nextPiece;
    }

    private int[][][] CreateRandomPiece() {
        //完全ランダムではなく全種類が同じ頻度で取得されるようにする
        if (_piecePool == null || _piecePool.Length == 0) {
            ResetPiecePool();
        }
        return getOnePiece();
    }

    // ==============================
    // Pieceクラスの定義
    // ==============================

    private int[][][] CreatePiece(int type, int[][] data, int[][] preview) {
        var piece = new int[3][][];
        piece[0] = new int[3][];
        piece[0][0] = new int[6];
        //type
        piece[0][0][0] = type;
        //angle
        piece[0][0][1] = Angle0;
        //data position
        piece[0][0][2] = PositionSpawnX;
        piece[0][0][3] = PositionSpawnY;
        //ghost position Y
        piece[0][0][4] = PositionSpawnY;
        //data
        piece[1] = data;
        //preview
        piece[2] = preview;
        return piece;
    }

    private int[][] GetPieceData(int[][][] piece) {
        return piece[1];
    }

    private void SetPieceData(int[][][] piece, int[][] data) {
        piece[1] = data;
    }

    private int[][] GetPiecePreview(int[][][] piece) {
        return piece[2];
    }

    private int GetPieceType(int[][][] piece) {
        return piece[0][0][0];
    }

    private int GetPieceAngle(int[][][] piece) {
        return piece[0][0][1];
    }

    private void SetPieceAngle(int[][][] piece, int angle) {
        piece[0][0][1] = angle;
    }

    private int GetPiecePosX(int[][][] piece) {
        return piece[0][0][2];
    }

    private void SetPiecePosX(int[][][] piece, int pos) {
        piece[0][0][2] = pos;
    }

    private int GetPiecePosY(int[][][] piece) {
        return piece[0][0][3];
    }

    private void SetPiecePosY(int[][][] piece, int pos) {
        piece[0][0][3] = pos;
    }

    private int GetPieceGhostPosY(int[][][] piece) {
        return piece[0][0][4];
    }

    private void SetPieceGhostPosY(int[][][] piece, int pos) {
        piece[0][0][4] = pos;
    }

    private int GetPieceSize(int[][] data) {
        return data.Length;
    }

    // ==============================
    // Pieceファクトリー
    // ==============================

    private int[][][] CreatePieceFromIndex(int index) {
        switch (index) {
            case PieceTypeI:
                //I
                // {0, 0, 0, 0}, // □ □ □ □
                // {1, 1, 1, 1}, // ■ ■ ■ ■
                // {0, 0, 0, 0}, // □ □ □ □
                // {0, 0, 0, 0}, // □ □ □ □ 
                var dataI = CreateIntArrayMxN(4, 4);
                dataI[1][0] = 1;
                dataI[1][1] = 1;
                dataI[1][2] = 1;
                dataI[1][3] = 1;
                var previewI = CreateIntArrayMxN(4, 4);
                previewI[1][0] = 1;
                previewI[1][1] = 1;
                previewI[1][2] = 1;
                previewI[1][3] = 1;
                return CreatePiece(PieceTypeI, dataI, previewI);
            case PieceTypeO:
                //O
                // {0, 0, 0, 0}, // □ □ □ □
                // {0, 2, 2, 0}, // □ ■ ■ □
                // {0, 2, 2, 0}, // □ ■ ■ □
                // {0, 0, 0, 0}, // □ □ □ □
                var dataO = CreateIntArrayMxN(4, 4);
                dataO[1][1] = 2;
                dataO[1][2] = 2;
                dataO[2][1] = 2;
                dataO[2][2] = 2;
                var previewO = CreateIntArrayMxN(4, 4);
                previewO[1][1] = 2;
                previewO[1][2] = 2;
                previewO[2][1] = 2;
                previewO[2][2] = 2;
                return CreatePiece(PieceTypeO, dataO, previewO);
            case PieceTypeS:
                //S
                // {0, 3, 3}, // □ ■ ■
                // {3, 3, 0}, // ■ ■ □
                // {0, 0, 0}, // □ □ □
                var dataS = CreateIntArrayMxN(3, 3);
                dataS[0][1] = 3;
                dataS[0][2] = 3;
                dataS[1][0] = 3;
                dataS[1][1] = 3;
                var previewS = CreateIntArrayMxN(4, 4);
                previewS[1][1] = 3;
                previewS[1][2] = 3;
                previewS[2][0] = 3;
                previewS[2][1] = 3;
                return CreatePiece(PieceTypeS, dataS, previewS);
            case PieceTypeZ:
                //Z
                // {4, 4, 0}, // ■ ■ □
                // {0, 4, 4}, // □ ■ ■
                // {0, 0, 0}, // □ □ □
                var dataZ = CreateIntArrayMxN(3, 3);
                dataZ[0][0] = 4;
                dataZ[0][1] = 4;
                dataZ[1][1] = 4;
                dataZ[1][2] = 4;
                var previewZ = CreateIntArrayMxN(4, 4);
                previewZ[1][0] = 4;
                previewZ[1][1] = 4;
                previewZ[2][1] = 4;
                previewZ[2][2] = 4;
                return CreatePiece(PieceTypeZ, dataZ, previewZ);
            case PieceTypeJ:
                //J
                // {0, 0, 5}, // □ □ ■
                // {5, 5, 5}, // ■ ■ ■
                // {0, 0, 0}, // □ □ □
                var dataJ = CreateIntArrayMxN(3, 3);
                dataJ[0][2] = 5;
                dataJ[1][0] = 5;
                dataJ[1][1] = 5;
                dataJ[1][2] = 5;
                var previewJ = CreateIntArrayMxN(4, 4);
                previewJ[1][2] = 5;
                previewJ[2][0] = 5;
                previewJ[2][1] = 5;
                previewJ[2][2] = 5;
                return CreatePiece(PieceTypeJ, dataJ, previewJ);
            case PieceTypeL:
                //L
                // {6, 0, 0}, // ■ □ □
                // {6, 6, 6}, // ■ ■ ■
                // {0, 0, 0}, // □ □ □
                var dataL = CreateIntArrayMxN(3, 3);
                dataL[0][0] = 6;
                dataL[1][0] = 6;
                dataL[1][1] = 6;
                dataL[1][2] = 6;
                var previewL = CreateIntArrayMxN(4, 4);
                previewL[1][0] = 6;
                previewL[2][0] = 6;
                previewL[2][1] = 6;
                previewL[2][2] = 6;
                return CreatePiece(PieceTypeL, dataL, previewL);
            case PieceTypeT:
                //T
                // {0, 7, 0}, // □ ■ □
                // {7, 7, 7}, // ■ ■ ■
                // {0, 0, 0}, // □ □ □
                var dataT = CreateIntArrayMxN(3, 3);
                dataT[0][1] = 7;
                dataT[1][0] = 7;
                dataT[1][1] = 7;
                dataT[1][2] = 7;
                var previewT = CreateIntArrayMxN(4, 4);
                previewT[1][1] = 7;
                previewT[2][0] = 7;
                previewT[2][1] = 7;
                previewT[2][2] = 7;
                return CreatePiece(PieceTypeT, dataT, previewT);
        }
        return null; //InvalidProgram
    }

    // ==============================
    // Array系ユーティリティ
    // ==============================

    private int[][] CreateIntArrayMxN(int m, int n) {
        var data = new int[m][];
        for (var i = 0; i < m; i++) {
            data[i] = new int[n];
        }
        return data;
    }

    private void ArrayCopyMxN(int[][] arrayFrom, int[][] arrayTo) {
        for (var i = 0; i < arrayFrom.Length; i++) {
            Array.Copy(arrayFrom[i], arrayTo[i], arrayFrom[i].Length);
        }
    }

    private void ArrayClearMxN(int[][] array) {
        foreach (var t in array) {
            Array.Clear(t, 0, t.Length);
        }
    }

    // ==============================
    // シリアライザ
    // ==============================

    private string SerializeField(int[][] field) {
        //更新カウント
        if (_updatePreviewCount < 63) {
            _updatePreviewCount++;
        } else {
            //0は受信側の初期化用で送信側は1から始める
            _updatePreviewCount = 1;
        }

        var temp = ConvertToBase64String(_updatePreviewCount);

        //Next
        temp += ConvertToBase64String(GetPieceType(_nextPiece[0]));
        temp += ConvertToBase64String(GetPieceType(_nextPiece[1]));
        temp += ConvertToBase64String(GetPieceType(_nextPiece[2]));
        temp += ConvertToBase64String(GetPieceType(_nextPiece[3]));

        //Hold
        if (_holdPiece == null) {
            temp += ConvertToBase64String(0);
        } else {
            temp += ConvertToBase64String(GetPieceType(_holdPiece));
        }

        if (_state == StateGameOver) {
            temp += "&";
        }

        for (var i = 0; i < MaxY - 5; i++) {
            for (var j = 0; j < MaxX; j += 2) {
                var val1Ghost = false;
                var val2Ghost = false;
                var val1Delete = false;
                var val2Delete = false;

                var val1 = field[i + 5][j];
                if (val1 > 8 && val1 < 16) {
                    //Ghost
                    val1Ghost = true;
                    val1 -= 8;
                } else if (val1 > 15 && val1 < 23) {
                    //Delete
                    val1Delete = true;
                    val1 -= 15;
                }
                var val2 = field[i + 5][j + 1];
                if (val2 > 8 && val2 < 16) {
                    //Ghost
                    val2Ghost = true;
                    val2 -= 8;
                } else if (val2 > 15 && val2 < 23) {
                    //Delete
                    val2Delete = true;
                    val2 -= 15;
                }
                if (val1Ghost && !val2Ghost) {
                    temp += "!";
                }
                if (!val1Ghost && val2Ghost) {
                    temp += "@";
                }
                if (val1Ghost && val2Ghost) {
                    temp += "#";
                }
                if (val1Delete && !val2Delete) {
                    temp += "$";
                }
                if (!val1Delete && val2Delete) {
                    temp += "%";
                }
                if (val1Delete && val2Delete) {
                    temp += "^";
                }
                if (val1 == 8 || val2 == 8) {
                    val1 = (val1 == 8) ? 1 : 0;
                    val2 = (val2 == 8) ? 1 : 0;
                }
                var val = (val1 << 3) + val2;
                temp += ConvertToBase64String(val);
            }
        }
        return temp;
    }

    private string ConvertToBase64String(int val) {
        switch (val) {
            case 0:
                return "A";
            case 1:
                return "B";
            case 2:
                return "C";
            case 3:
                return "D";
            case 4:
                return "E";
            case 5:
                return "F";
            case 6:
                return "G";
            case 7:
                return "H";
            case 8:
                return "I";
            case 9:
                return "J";
            case 10:
                return "K";
            case 11:
                return "L";
            case 12:
                return "M";
            case 13:
                return "N";
            case 14:
                return "O";
            case 15:
                return "P";
            case 16:
                return "Q";
            case 17:
                return "R";
            case 18:
                return "S";
            case 19:
                return "T";
            case 20:
                return "U";
            case 21:
                return "V";
            case 22:
                return "W";
            case 23:
                return "X";
            case 24:
                return "Y";
            case 25:
                return "Z";
            case 26:
                return "a";
            case 27:
                return "b";
            case 28:
                return "c";
            case 29:
                return "d";
            case 30:
                return "e";
            case 31:
                return "f";
            case 32:
                return "g";
            case 33:
                return "h";
            case 34:
                return "i";
            case 35:
                return "j";
            case 36:
                return "k";
            case 37:
                return "l";
            case 38:
                return "m";
            case 39:
                return "n";
            case 40:
                return "o";
            case 41:
                return "p";
            case 42:
                return "q";
            case 43:
                return "r";
            case 44:
                return "s";
            case 45:
                return "t";
            case 46:
                return "u";
            case 47:
                return "v";
            case 48:
                return "w";
            case 49:
                return "x";
            case 50:
                return "y";
            case 51:
                return "z";
            case 52:
                return "0";
            case 53:
                return "1";
            case 54:
                return "2";
            case 55:
                return "3";
            case 56:
                return "4";
            case 57:
                return "5";
            case 58:
                return "6";
            case 59:
                return "7";
            case 60:
                return "8";
            case 61:
                return "9";
            case 62:
                return "+";
            default:
                return "/";
        }
    }
}