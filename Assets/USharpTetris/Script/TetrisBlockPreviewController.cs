using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TetrisBlockPreviewController : UdonSharpBehaviour {
    private const int MaxY = 20;
    private const int MaxX = 10;
    private const int PieceTypeNone = 0;
    private const int PieceTypeI = 1;
    private const int PieceTypeO = 2;
    private const int PieceTypeS = 3;
    private const int PieceTypeZ = 4;
    private const int PieceTypeJ = 5;
    private const int PieceTypeL = 6;
    private const int PieceTypeT = 7;
    private const int PieceTypeGray = 8;
    private const int StatePause = 0;
    private const int StateRestart = 1;
    private const int StatePlaying = 2;
    private const int StateGameOver = 3;

    private const string ClearField =
        "000000AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public GameObject materials;
    public GameObject boardRoot;
    public GameObject previewNextPieces;
    public GameObject holdPiece;
    public int playerIndex;
    public TextMeshPro playerNameField;
    public TextMeshPro scoreField;

    private Material[] _colorsCache;
    private GameObject[][] _boardBlocksGameObjectCache;
    private MeshRenderer[][] _boardBlocksMeshRendererCache;
    private MeshRenderer[][][] _previewNextPiecesMeshRendererCache;
    private MeshRenderer[][] _holdPiecesMeshRendererCache;
    private string _updateCountStr;
    private int[][] _currentField;
    private int[][][] _previewPieces;
    private int[] _nextPiece;
    private int _holdPiece;

    private string _currentUserName;
    private bool _playerUpdate;

    [UdonSynced] private string _fieldString;
    [UdonSynced] private long _score;

    private void Start() {
        InitMaterials();
        InitBoardBlocks();
        InitPreviewBlocks();
        InitPreviewPieces();
        InitHoldPiece();

        _updateCountStr = "0";
        _currentField = CreateIntArrayMxN(MaxY, MaxX);
        _nextPiece = new int[4];
        _holdPiece = 0;

        _fieldString = ClearField;
        _playerUpdate = false;

        UpdatePlayerName();

        Deserialize();

        //ボードを再描画
        RedrawBoardBlocks();
    }

    private void InitMaterials() {
        _colorsCache = new Material[materials.transform.childCount];
        for (var i = 0; i < _colorsCache.Length; i++) {
            _colorsCache[i] = GetMaterial(i);
        }
    }

    /// <summary>
    /// マテリアルの取得
    /// Shader.FindやResourceの直接取得ができないのでGameObjectのRendererを経由して取得する
    /// </summary>
    /// <param name="index">マテリアルのインデックス</param>
    /// <returns>マテリアル</returns>
    private Material GetMaterial(int index) {
        return materials.transform.GetChild(index).GetComponent<MeshRenderer>().sharedMaterial;
    }

    private void InitBoardBlocks() {
        _boardBlocksGameObjectCache = new GameObject[MaxY][];
        _boardBlocksMeshRendererCache = new MeshRenderer[MaxY][];
        for (var i = 0; i < MaxY; i++) {
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

    private void InitPreviewPieces() {
        _previewPieces = new int[8][][];
        for (var i = 0; i < 8; i++) {
            switch (i) {
                case PieceTypeNone:
                    var previewNone = CreateIntArrayMxN(4, 4);
                    _previewPieces[i] = previewNone;
                    break;
                case PieceTypeI:
                    //I
                    // {0, 0, 0, 0}, // □ □ □ □
                    // {1, 1, 1, 1}, // ■ ■ ■ ■
                    // {0, 0, 0, 0}, // □ □ □ □
                    // {0, 0, 0, 0}, // □ □ □ □ 
                    var previewI = CreateIntArrayMxN(4, 4);
                    previewI[1][0] = 1;
                    previewI[1][1] = 1;
                    previewI[1][2] = 1;
                    previewI[1][3] = 1;
                    _previewPieces[i] = previewI;
                    break;
                case PieceTypeO:
                    //O
                    // {0, 0, 0, 0}, // □ □ □ □
                    // {0, 2, 2, 0}, // □ ■ ■ □
                    // {0, 2, 2, 0}, // □ ■ ■ □
                    // {0, 0, 0, 0}, // □ □ □ □
                    var previewO = CreateIntArrayMxN(4, 4);
                    previewO[1][1] = 2;
                    previewO[1][2] = 2;
                    previewO[2][1] = 2;
                    previewO[2][2] = 2;
                    _previewPieces[i] = previewO;
                    break;
                case PieceTypeS:
                    //S
                    // {0, 3, 3}, // □ ■ ■
                    // {3, 3, 0}, // ■ ■ □
                    // {0, 0, 0}, // □ □ □
                    var previewS = CreateIntArrayMxN(4, 4);
                    previewS[1][1] = 3;
                    previewS[1][2] = 3;
                    previewS[2][0] = 3;
                    previewS[2][1] = 3;
                    _previewPieces[i] = previewS;
                    break;
                case PieceTypeZ:
                    //Z
                    // {4, 4, 0}, // ■ ■ □
                    // {0, 4, 4}, // □ ■ ■
                    // {0, 0, 0}, // □ □ □
                    var previewZ = CreateIntArrayMxN(4, 4);
                    previewZ[1][0] = 4;
                    previewZ[1][1] = 4;
                    previewZ[2][1] = 4;
                    previewZ[2][2] = 4;
                    _previewPieces[i] = previewZ;
                    break;
                case PieceTypeJ:
                    //J
                    // {0, 0, 5}, // □ □ ■
                    // {5, 5, 5}, // ■ ■ ■
                    // {0, 0, 0}, // □ □ □
                    var previewJ = CreateIntArrayMxN(4, 4);
                    previewJ[1][2] = 5;
                    previewJ[2][0] = 5;
                    previewJ[2][1] = 5;
                    previewJ[2][2] = 5;
                    _previewPieces[i] = previewJ;
                    break;
                case PieceTypeL:
                    //L
                    // {6, 0, 0}, // ■ □ □
                    // {6, 6, 6}, // ■ ■ ■
                    // {0, 0, 0}, // □ □ □
                    var previewL = CreateIntArrayMxN(4, 4);
                    previewL[1][0] = 6;
                    previewL[2][0] = 6;
                    previewL[2][1] = 6;
                    previewL[2][2] = 6;
                    _previewPieces[i] = previewL;
                    break;
                case PieceTypeT:
                    //T
                    // {0, 7, 0}, // □ ■ □
                    // {7, 7, 7}, // ■ ■ ■
                    // {0, 0, 0}, // □ □ □
                    var previewT = CreateIntArrayMxN(4, 4);
                    previewT[1][1] = 7;
                    previewT[2][0] = 7;
                    previewT[2][1] = 7;
                    previewT[2][2] = 7;
                    _previewPieces[i] = previewT;
                    break;
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

    public override void OnPlayerJoined(VRCPlayerApi player) {
        var players = new VRCPlayerApi[20];
        var vrcPlayerApis = PlayerIdSort(VRCPlayerApi.GetPlayers(players));
        var vrcPlayerApi = vrcPlayerApis[playerIndex];
        if (vrcPlayerApi != null && vrcPlayerApi.IsValid()) {
            if (_currentUserName != vrcPlayerApi.displayName) {
                _currentUserName = vrcPlayerApi.displayName;
                _playerUpdate = true;
                gameObject.transform.parent.GetChild(1).gameObject.SetActive(true);
            }
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player) {
        var players = new VRCPlayerApi[20];
        var vrcPlayerApis = PlayerIdSort(VRCPlayerApi.GetPlayers(players));
        var vrcPlayerApi = vrcPlayerApis[playerIndex];
        if (vrcPlayerApi != null && vrcPlayerApi.IsValid()) {
            if (_currentUserName != vrcPlayerApi.displayName) {
                _currentUserName = vrcPlayerApi.displayName;
                _playerUpdate = true;
                gameObject.transform.parent.GetChild(1).gameObject.SetActive(true);
            }
        }
        if (vrcPlayerApi == null || !vrcPlayerApi.IsValid()) {
            _playerUpdate = true;
            gameObject.transform.parent.GetChild(1).gameObject.SetActive(false);
        }
    }

    private void Update() {
        //更新されている場合は再描画
        if (_updateCountStr != GetUpdateCountStr()) {
            //スコアの更新
            UpdateScore();
            //フィールドを更新
            Deserialize();
            RedrawBoardBlocks();
            RedrawNextPieces();
            RedrawHoldPiece();
        }

        //プレイヤーが追加、変更された場合
        if (_playerUpdate) {
            _playerUpdate = false;
            UpdatePlayerName();
            ClearBoardBlocks();
        }
    }

    private void UpdateScore() {
        scoreField.text = $"Score: {_score}";
    }

    private void UpdatePlayerName() {
        var players = new VRCPlayerApi[20];
        var vrcPlayerApis = PlayerIdSort(VRCPlayerApi.GetPlayers(players));
        var vrcPlayerApi = vrcPlayerApis[playerIndex];
        if (vrcPlayerApi != null && vrcPlayerApi.IsValid()) {
            scoreField.text = $"Score: {_score}";
            playerNameField.text = $"{vrcPlayerApi.playerId}: {vrcPlayerApi.displayName}";
        } else {
            _score = 0;
            _fieldString = ClearField;
            RequestSerialization();
            scoreField.text = $"";
            playerNameField.text = "";
        }
    }

    private void RedrawBoardBlocks() {
        //現在のピースを表示用にフィールドに合成
        for (var i = 0; i < MaxY; i++) {
            for (var j = 0; j < MaxX; j++) {
                var blockType = _currentField[i][j];
                if (blockType == 0) {
                    _boardBlocksGameObjectCache[i][j].SetActive(false);
                } else {
                    SetMaterial(_boardBlocksMeshRendererCache[i][j], blockType);
                    _boardBlocksGameObjectCache[i][j].SetActive(true);
                }
            }
        }
    }

    private void ClearBoardBlocks() {
        //現在のピースを表示用にフィールドに合成
        for (var i = 0; i < MaxY; i++) {
            for (var j = 0; j < MaxX; j++) {
                _boardBlocksGameObjectCache[i][j].SetActive(false);
            }
        }
    }

    private void RedrawNextPieces() {
        for (var i = 0; i < 4; i++) {
            var preview = _previewPieces[_nextPiece[i]];
            for (var j = 0; j < 4; j++) {
                for (var k = 0; k < 4; k++) {
                    SetMaterial(_previewNextPiecesMeshRendererCache[i][j][k], preview[j][k]);
                }
            }
        }
    }

    private void RedrawHoldPiece() {
        var preview = _previewPieces[_holdPiece];
        for (var j = 0; j < 4; j++) {
            for (var k = 0; k < 4; k++) {
                SetMaterial(_holdPiecesMeshRendererCache[j][k], preview[j][k]);
            }
        }
    }

    private void SetMaterial(MeshRenderer render, int blockType) {
        if (render.sharedMaterial != _colorsCache[blockType]) {
            render.sharedMaterial = _colorsCache[blockType];
        }
    }

    public void UpdateField(string field, long score) {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (Networking.IsOwner(Networking.LocalPlayer, gameObject)) {
            _fieldString = field;
            _score = score;
            RequestSerialization();
        }
    }

    // ==============================
    // デシリアライズ
    // ==============================

    private string GetUpdateCountStr() {
        return _fieldString.Substring(0, 1);
    }

    private void Deserialize() {
        var temp = _fieldString;
        var index = 0;

        //更新カウント
        _updateCountStr = GetUpdateCountStr();
        index++;

        //Next
        _nextPiece[0] = ConvertToByte(temp.Substring(index, 1));
        index++;
        _nextPiece[1] = ConvertToByte(temp.Substring(index, 1));
        index++;
        _nextPiece[2] = ConvertToByte(temp.Substring(index, 1));
        index++;
        _nextPiece[3] = ConvertToByte(temp.Substring(index, 1));
        index++;

        //Hold
        _holdPiece = ConvertToByte(temp.Substring(index, 1));
        index++;

        var isGameOver = temp.Substring(index, 1) == "&";
        if (isGameOver) {
            index++;
        }

        //Field
        for (var i = 0; i < MaxY; i++) {
            for (var j = 0; j < MaxX; j += 2) {
                var base64Str = temp.Substring(index, 1);
                if (isGameOver) {
                    var ba = ConvertToByte(base64Str);
                    _currentField[i][j] = (ba >> 3) > 0 ? 8 : 0;
                    _currentField[i][j + 1] = (ba % 8) > 0 ? 8 : 0;
                    index++;
                } else {
                    var fix1 = 0;
                    var fix2 = 0;
                    if (base64Str == "!") {
                        fix1 += 8;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    if (base64Str == "@") {
                        fix2 += 8;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    if (base64Str == "#") {
                        fix1 += 8;
                        fix2 += 8;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    if (base64Str == "$") {
                        fix1 += 15;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    if (base64Str == "%") {
                        fix2 += 15;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    if (base64Str == "^") {
                        fix1 += 15;
                        fix2 += 15;
                        index++;
                        base64Str = temp.Substring(index, 1);
                    }
                    var ba = ConvertToByte(base64Str);
                    _currentField[i][j] = (ba >> 3) + fix1;
                    _currentField[i][j + 1] = (ba % 8) + fix2;
                    index++;
                }
            }
        }
    }

    private byte ConvertToByte(string str) {
        switch (str) {
            case "A":
                return 0;
            case "B":
                return 1;
            case "C":
                return 2;
            case "D":
                return 3;
            case "E":
                return 4;
            case "F":
                return 5;
            case "G":
                return 6;
            case "H":
                return 7;
            case "I":
                return 8;
            case "J":
                return 9;
            case "K":
                return 10;
            case "L":
                return 11;
            case "M":
                return 12;
            case "N":
                return 13;
            case "O":
                return 14;
            case "P":
                return 15;
            case "Q":
                return 16;
            case "R":
                return 17;
            case "S":
                return 18;
            case "T":
                return 19;
            case "U":
                return 20;
            case "V":
                return 21;
            case "W":
                return 22;
            case "X":
                return 23;
            case "Y":
                return 24;
            case "Z":
                return 25;
            case "a":
                return 26;
            case "b":
                return 27;
            case "c":
                return 28;
            case "d":
                return 29;
            case "e":
                return 30;
            case "f":
                return 31;
            case "g":
                return 32;
            case "h":
                return 33;
            case "i":
                return 34;
            case "j":
                return 35;
            case "k":
                return 36;
            case "l":
                return 37;
            case "m":
                return 38;
            case "n":
                return 39;
            case "o":
                return 40;
            case "p":
                return 41;
            case "q":
                return 42;
            case "r":
                return 43;
            case "s":
                return 44;
            case "t":
                return 45;
            case "u":
                return 46;
            case "v":
                return 47;
            case "w":
                return 48;
            case "x":
                return 49;
            case "y":
                return 50;
            case "z":
                return 51;
            case "0":
                return 52;
            case "1":
                return 53;
            case "2":
                return 54;
            case "3":
                return 55;
            case "4":
                return 56;
            case "5":
                return 57;
            case "6":
                return 58;
            case "7":
                return 59;
            case "8":
                return 60;
            case "9":
                return 61;
            case "+":
                return 62;
            default:
                return 63;
        }
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
}