using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace APlusPlusChecker;

public enum TokenType
{
    IntLiteral, FloatLiteral, StringLiteral, BoolLiteral,
    Bool, Else, Float, For, If, Int, Return, String, Textout, Void, While,
    Plus, Minus, Star, Slash, Percent, Eq, Lt, Gt, Bang,
    Amp, Pipe, LParen, RParen, LBrace, RBrace, Comma, Semicolon,
    PlusPlus, MinusMinus, PlusEq, MinusEq, StarEq, SlashEq, PercentEq,
    AmpAmp, PipePipe, EqEq, BangEq, LtEq, GtEq,
    Identifier, NewLine, Eof, Unknown
}

public class Token
{
    public TokenType Type;
    public string    Value;
    public int       Line;
    public int       Col;
    public Token(TokenType t, string v, int ln, int col)
    { Type = t; Value = v; Line = ln; Col = col; }
}

public class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        {"bool",TokenType.Bool},{"else",TokenType.Else},{"false",TokenType.BoolLiteral},
        {"float",TokenType.Float},{"for",TokenType.For},{"if",TokenType.If},
        {"int",TokenType.Int},{"return",TokenType.Return},{"string",TokenType.String},
        {"Textout",TokenType.Textout},{"true",TokenType.BoolLiteral},
        {"void",TokenType.Void},{"while",TokenType.While}
    };
    private static readonly Dictionary<string, TokenType> CompoundOps = new()
    {
        {"++",TokenType.PlusPlus},{"--",TokenType.MinusMinus},{"+=",TokenType.PlusEq},
        {"-=",TokenType.MinusEq},{"*=",TokenType.StarEq},{"/=",TokenType.SlashEq},
        {"%=",TokenType.PercentEq},{"&&",TokenType.AmpAmp},{"||",TokenType.PipePipe},
        {"==",TokenType.EqEq},{"!=",TokenType.BangEq},{"<=",TokenType.LtEq},{">=",TokenType.GtEq}
    };
    private static readonly Dictionary<char, TokenType> SingleOps = new()
    {
        {'+',TokenType.Plus},{'-',TokenType.Minus},{'*',TokenType.Star},
        {'/',TokenType.Slash},{'%',TokenType.Percent},{'=',TokenType.Eq},
        {'<',TokenType.Lt},{'>',TokenType.Gt},{'!',TokenType.Bang},
        {'&',TokenType.Amp},{'|',TokenType.Pipe},{'(',TokenType.LParen},
        {')',TokenType.RParen},{'{',TokenType.LBrace},{'}',TokenType.RBrace},
        {',',TokenType.Comma},{';',TokenType.Semicolon}
    };

    public List<Token> Tokens = new();
    public string?     Error  = null;

    public bool Tokenize(string source)
    {
        Tokens.Clear(); Error = null;
        var lines = source.Split('\n');
        for (int ln = 0; ln < lines.Length; ln++)
        {
            string line = lines[ln];
            int i = 0;
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i])) { i++; continue; }
                if (i + 1 < line.Length && line[i] == '/' && line[i+1] == '/') break;

                if (line[i] == '"')
                {
                    int j = i + 1;
                    while (j < line.Length && line[j] != '"') j++;
                    if (j >= line.Length) { Error = $"Line {ln+1}: unterminated string literal"; return false; }
                    Tokens.Add(new Token(TokenType.StringLiteral, line.Substring(i, j-i+1), ln+1, i+1));
                    i = j + 1; continue;
                }

                var floatM = Regex.Match(line[i..], @"^[0-9]+\.[0-9]+");
                if (floatM.Success) { Tokens.Add(new Token(TokenType.FloatLiteral, floatM.Value, ln+1, i+1)); i += floatM.Length; continue; }

                var intM = Regex.Match(line[i..], @"^[0-9]+");
                if (intM.Success) { Tokens.Add(new Token(TokenType.IntLiteral, intM.Value, ln+1, i+1)); i += intM.Length; continue; }

                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int j = i;
                    while (j < line.Length && (char.IsLetterOrDigit(line[j]) || line[j] == '_')) j++;
                    string word = line.Substring(i, j-i);
                    TokenType type = Keywords.TryGetValue(word, out var kw) ? kw : TokenType.Identifier;
                    Tokens.Add(new Token(type, word, ln+1, i+1));
                    i = j; continue;
                }

                if (i + 1 < line.Length)
                {
                    string two = line.Substring(i, 2);
                    if (CompoundOps.TryGetValue(two, out var cop)) { Tokens.Add(new Token(cop, two, ln+1, i+1)); i += 2; continue; }
                }

                if (SingleOps.TryGetValue(line[i], out var sop)) { Tokens.Add(new Token(sop, line[i].ToString(), ln+1, i+1)); i++; continue; }

                Error = $"Line {ln+1}, col {i+1}: unexpected character '{line[i]}'"; return false;
            }
            bool hasContent = Tokens.Count > 0 && Tokens[^1].Type != TokenType.NewLine;
            if (hasContent) Tokens.Add(new Token(TokenType.NewLine, "↵", ln+1, line.Length+1));
        }
        Tokens.Add(new Token(TokenType.Eof, "EOF", lines.Length, 0));
        return true;
    }
}

public class Parser
{
    private List<Token> _tokens = new();
    private int         _pos;
    public  List<string> Errors = new();

    private static readonly HashSet<TokenType> TypeKw = new()
        { TokenType.Int, TokenType.Float, TokenType.String, TokenType.Bool, TokenType.Void };

    public List<string> Parse(List<Token> tokens)
    {
        _tokens = tokens; _pos = 0; Errors.Clear();
        ParseProgram();
        return Errors;
    }

    private Token Peek()
    {
        while (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.NewLine) _pos++;
        return _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenType.Eof, "EOF", 0, 0);
    }
    private Token Consume() => _tokens[_pos++];
    private bool IsType() => TypeKw.Contains(Peek().Type);
    private void Expect(TokenType t, string ctx = "")
    {
        var tok = Peek();
        if (tok.Type == t) { Consume(); return; }
        Errors.Add($"Line {tok.Line}: expected '{TStr(t)}'" + (ctx.Length > 0 ? $" in {ctx}" : "") + $", got '{tok.Value}'");
    }
    private static string TStr(TokenType t) => t switch
    {
        TokenType.LParen => "(", TokenType.RParen => ")",
        TokenType.LBrace => "{", TokenType.RBrace => "}",
        TokenType.Semicolon => ";", TokenType.Comma => ",",
        _ => t.ToString()
    };

    private void ParseProgram()
    {
        while (Peek().Type != TokenType.Eof)
        {
            if (!IsType()) { Errors.Add($"Line {Peek().Line}: expected type keyword, got '{Peek().Value}'"); Consume(); continue; }
            ParseFuncDecl();
        }
    }
    private void ParseFuncDecl()
    {
        Consume();
        if (Peek().Type != TokenType.Identifier) Errors.Add($"Line {Peek().Line}: expected function name");
        else Consume();
        ParseParams(); ParseBlock();
    }
    private void ParseParams()
    {
        Expect(TokenType.LParen, "params");
        while (IsType())
        {
            Consume();
            if (Peek().Type != TokenType.Identifier) Errors.Add($"Line {Peek().Line}: expected parameter name");
            else Consume();
            if (Peek().Type == TokenType.Comma) Consume(); else break;
        }
        Expect(TokenType.RParen, "params");
    }
    private void ParseBlock()
    {
        Expect(TokenType.LBrace, "block");
        while (Peek().Type != TokenType.RBrace && Peek().Type != TokenType.Eof) ParseStmt();
        Expect(TokenType.RBrace, "block");
    }
    private void ParseStmt()
    {
        var t = Peek();
        if (t.Type == TokenType.Eof || t.Type == TokenType.RBrace) return;
        if (t.Type == TokenType.If)     { ParseIf();     return; }
        if (t.Type == TokenType.While)  { ParseWhile();  return; }
        if (t.Type == TokenType.For)    { ParseFor();    return; }
        if (t.Type == TokenType.Return) { ParseReturn(); return; }
        if (t.Type == TokenType.Textout){ ParseTextout();return; }
        if (IsType()) { ParseVarDec(); return; }
        ParseExpr();
    }
    private void ParseIf()
    {
        Consume(); Expect(TokenType.LParen,"if"); ParseExpr(); Expect(TokenType.RParen,"if");
        ParseBlock();
        if (Peek().Type == TokenType.Else) { Consume(); ParseBlock(); }
    }
    private void ParseWhile()
    {
        Consume(); Expect(TokenType.LParen,"while"); ParseExpr(); Expect(TokenType.RParen,"while");
        ParseBlock();
    }
    private void ParseFor()
    {
        Consume(); Expect(TokenType.LParen,"for");
        if (IsType()) ParseVarDec(); else if (Peek().Type != TokenType.Semicolon) ParseExpr();
        Expect(TokenType.Semicolon,"for init");
        if (Peek().Type != TokenType.Semicolon) ParseExpr();
        Expect(TokenType.Semicolon,"for cond");
        if (Peek().Type != TokenType.RParen) ParseExpr();
        Expect(TokenType.RParen,"for"); ParseBlock();
    }
    private void ParseReturn()
    {
        Consume();
        var n = Peek();
        if (n.Type != TokenType.NewLine && n.Type != TokenType.Eof && n.Type != TokenType.RBrace) ParseExpr();
    }
    private void ParseTextout() { Consume(); Expect(TokenType.LParen,"Textout"); ParseExpr(); Expect(TokenType.RParen,"Textout"); }
    private void ParseVarDec()
    {
        Consume();
        if (Peek().Type != TokenType.Identifier) Errors.Add($"Line {Peek().Line}: expected identifier in declaration");
        else Consume();
        if (Peek().Type == TokenType.Eq) { Consume(); ParseExpr(); }
    }
    private void ParseExpr()   => ParseAssign();
    private void ParseAssign()
    {
        ParseOr();
        var ops = new HashSet<TokenType> { TokenType.Eq, TokenType.PlusEq, TokenType.MinusEq, TokenType.StarEq, TokenType.SlashEq, TokenType.PercentEq };
        if (ops.Contains(Peek().Type)) { Consume(); ParseAssign(); }
    }
    private void ParseOr()     { ParseAnd();     while (Peek().Type == TokenType.PipePipe) { Consume(); ParseAnd(); } }
    private void ParseAnd()    { ParseCmp();     while (Peek().Type == TokenType.AmpAmp)   { Consume(); ParseCmp(); } }
    private void ParseCmp()
    {
        ParseAdd();
        var ops = new HashSet<TokenType> { TokenType.EqEq, TokenType.BangEq, TokenType.Lt, TokenType.Gt, TokenType.LtEq, TokenType.GtEq };
        while (ops.Contains(Peek().Type)) { Consume(); ParseAdd(); }
    }
    private void ParseAdd()    { ParseMult();    while (Peek().Type == TokenType.Plus  || Peek().Type == TokenType.Minus)  { Consume(); ParseMult(); } }
    private void ParseMult()   { ParseNeg();     while (Peek().Type == TokenType.Star  || Peek().Type == TokenType.Slash || Peek().Type == TokenType.Percent) { Consume(); ParseNeg(); } }
    private void ParseNeg()    { if (Peek().Type == TokenType.Minus || Peek().Type == TokenType.Bang) { Consume(); ParseNeg(); } else ParsePost(); }
    private void ParsePost()   { ParsePrimary(); while (Peek().Type == TokenType.PlusPlus || Peek().Type == TokenType.MinusMinus) Consume(); }
    private void ParsePrimary()
    {
        var t = Peek();
        if (t.Type == TokenType.IntLiteral || t.Type == TokenType.FloatLiteral ||
            t.Type == TokenType.StringLiteral || t.Type == TokenType.BoolLiteral) { Consume(); return; }
        if (t.Type == TokenType.Identifier) { Consume(); if (Peek().Type == TokenType.LParen) ParseCallArgs(); return; }
        if (t.Type == TokenType.LParen)     { Consume(); ParseExpr(); Expect(TokenType.RParen,"expr"); return; }
        Errors.Add($"Line {t.Line}: unexpected token '{t.Value}' in expression"); Consume();
    }
    private void ParseCallArgs()
    {
        Expect(TokenType.LParen,"call");
        if (Peek().Type != TokenType.RParen) { ParseExpr(); while (Peek().Type == TokenType.Comma) { Consume(); ParseExpr(); } }
        Expect(TokenType.RParen,"call");
    }
}

public class MainWindow : Window
{
    private TextBox   _editor   = null!;
    private TextBox   _tokenOut = null!;
    private TextBox   _parseOut = null!;
    private TextBlock _status   = null!;

    private static readonly Dictionary<string, string> Samples = new()
    {
        ["Hello World"] =
@"void main()
{
  string msg = ""Hello, World!""
  Textout(msg)
}",
        ["Function"] =
@"int add(int a, int b)
{
  return a + b
}

void main()
{
  int result = add(3, 4)
  Textout(result)
}",
        ["If / Else"] =
@"void main()
{
  int x = 10
  if (x > 5)
  {
    Textout(""big"")
  }
  else
  {
    Textout(""small"")
  }
}",
        ["While Loop"] =
@"void main()
{
  int i = 0
  while (i < 10)
  {
    Textout(i)
    i++
  }
}",
        ["For Loop"] =
@"void main()
{
  for (int i = 0; i < 5; i++)
  {
    Textout(i)
  }
}",
        ["Syntax Error"] =
@"void main()
{
  int = 42
  Textout(
}"
    };

    public MainWindow()
    {
        Title      = "A++ Grammar Checker  ·  GOLD Parser v5.0";
        Width      = 1100;
        Height     = 680;
        Background = new SolidColorBrush(Color.Parse("#12121a"));
        FontFamily = new FontFamily("Consolas,Monospace");
        Content    = BuildUI();
    }

    private Control BuildUI()
    {
        // ── header ──────────────────────────────────────────
        var header = new Grid
        {
            Background        = new SolidColorBrush(Color.Parse("#1c1c24")),
            Height            = 48,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto")
        };

        var badge = MakeLabel("A++", "#6366f1", 13, bold: true);
        badge.Margin = new Thickness(14, 0, 8, 0);
        badge.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(badge, 0);

        var sub = MakeLabel("GOLD Parser v5.0 · Abdullah Soliman", "#64748b", 9);
        sub.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sub, 1);

        var combo = new ComboBox
        {
            PlaceholderText     = "── Samples ──",
            Width               = 160,
            Margin              = new Thickness(0, 10, 8, 10),
            VerticalAlignment   = VerticalAlignment.Center
        };
        foreach (var k in Samples.Keys) combo.Items.Add(k);
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedItem is string name) { LoadSample(name); combo.SelectedItem = null; }
        };
        Grid.SetColumn(combo, 3);

        var btnClear = MakeButton("Clear", "#26263a", "#94a3b8");
        btnClear.Click += (_, _) => ClearAll();
        Grid.SetColumn(btnClear, 4);

        var btnParse = MakeButton("▶  Parse", "#6366f1", "#ffffff");
        btnParse.Click += (_, _) => RunParse();
        Grid.SetColumn(btnParse, 5);

        header.Children.Add(badge);
        header.Children.Add(sub);
        header.Children.Add(combo);
        header.Children.Add(btnClear);
        header.Children.Add(btnParse);

        // ── editor (left) ────────────────────────────────────
        _editor = new TextBox
        {
            AcceptsReturn   = true,
            AcceptsTab      = true,
            FontFamily      = new FontFamily("Consolas,Monospace"),
            FontSize        = 13,
            Background      = new SolidColorBrush(Color.Parse("#12121a")),
            Foreground      = new SolidColorBrush(Color.Parse("#e2e8f0")),
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(12, 8),
            TextWrapping    = TextWrapping.NoWrap,
            Watermark       = "Type A++ code here…  (Ctrl+Enter to parse)"
        };
        _editor.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control) RunParse();
        };

        var editorPane = new DockPanel { Background = new SolidColorBrush(Color.Parse("#12121a")) };
        editorPane.Children.Add(MakePaneLabel("SOURCE CODE"));
        editorPane.Children.Add(_editor);

        // ── token output (top-right) ─────────────────────────
        _tokenOut = new TextBox
        {
            IsReadOnly      = true,
            AcceptsReturn   = true,
            FontFamily      = new FontFamily("Consolas,Monospace"),
            FontSize        = 11,
            Background      = new SolidColorBrush(Color.Parse("#1c1c24")),
            Foreground      = new SolidColorBrush(Color.Parse("#e2e8f0")),
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(10, 6),
            TextWrapping    = TextWrapping.NoWrap
        };
        var tokenPane = new DockPanel { Background = new SolidColorBrush(Color.Parse("#1c1c24")) };
        tokenPane.Children.Add(MakePaneLabel("TOKENS"));
        tokenPane.Children.Add(_tokenOut);

        // ── parse result (bottom-right) ──────────────────────
        _parseOut = new TextBox
        {
            IsReadOnly      = true,
            AcceptsReturn   = true,
            FontFamily      = new FontFamily("Consolas,Monospace"),
            FontSize        = 11,
            Background      = new SolidColorBrush(Color.Parse("#1c1c24")),
            Foreground      = new SolidColorBrush(Color.Parse("#e2e8f0")),
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(10, 6),
            TextWrapping    = TextWrapping.Wrap
        };
        var parsePane = new DockPanel { Background = new SolidColorBrush(Color.Parse("#1c1c24")) };
        parsePane.Children.Add(MakePaneLabel("PARSE RESULT"));
        parsePane.Children.Add(_parseOut);

        // ── right panel (no splitter) ─────────────────────────
        var rightSplit = new Grid
        {
            RowDefinitions = new RowDefinitions("*,*"),
            Background     = new SolidColorBrush(Color.Parse("#1c1c24"))
        };
        Grid.SetRow(tokenPane, 0);
        Grid.SetRow(parsePane, 1);
        rightSplit.Children.Add(tokenPane);
        rightSplit.Children.Add(parsePane);

        // ── main layout (no splitter) ─────────────────────────
        var mainSplit = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,420"),
            Background        = new SolidColorBrush(Color.Parse("#12121a"))
        };
        Grid.SetColumn(editorPane,  0);
        Grid.SetColumn(rightSplit,  1);
        mainSplit.Children.Add(editorPane);
        mainSplit.Children.Add(rightSplit);

        // ── status bar ───────────────────────────────────────
        _status = new TextBlock
        {
            Text       = "  Ready",
            Foreground = new SolidColorBrush(Color.Parse("#64748b")),
            FontSize   = 11,
            Padding    = new Thickness(8, 4),
            Background = new SolidColorBrush(Color.Parse("#1c1c24"))
        };

        // ── root layout ──────────────────────────────────────
        var root = new DockPanel { Background = new SolidColorBrush(Color.Parse("#12121a")) };
        DockPanel.SetDock(header,  Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(_status);
        root.Children.Add(mainSplit);

        LoadSample("Hello World");
        return root;
    }

    private static TextBlock MakeLabel(string text, string hex, double size, bool bold = false) =>
        new TextBlock
        {
            Text       = text,
            Foreground = new SolidColorBrush(Color.Parse(hex)),
            FontSize   = size,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
        };

    private static TextBlock MakePaneLabel(string text) =>
        new TextBlock
        {
            Text       = text,
            Foreground = new SolidColorBrush(Color.Parse("#475569")),
            FontSize   = 9,
            FontWeight = FontWeight.Bold,
            Padding    = new Thickness(10, 6, 0, 2)
        };

    private static Button MakeButton(string text, string bg, string fg)
    {
        var b = new Button
        {
            Content         = text,
            Background      = new SolidColorBrush(Color.Parse(bg)),
            Foreground      = new SolidColorBrush(Color.Parse(fg)),
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(16, 6),
            Margin          = new Thickness(4, 10, 4, 10),
            FontSize        = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor          = new Cursor(StandardCursorType.Hand)
        };
        return b;
    }

    private void LoadSample(string name)
    {
        if (!Samples.ContainsKey(name)) return;
        _editor.Text = Samples[name];
        RunParse();
    }

    private void ClearAll()
    {
        _editor.Text   = "";
        _tokenOut.Text = "";
        _parseOut.Text = "";
        SetStatus("Ready", "#64748b");
    }

    private void RunParse()
    {
        string src = _editor.Text ?? "";
        if (string.IsNullOrWhiteSpace(src)) { SetStatus("Nothing to parse", "#64748b"); return; }

        var lexer = new Lexer();
        if (!lexer.Tokenize(src))
        {
            ShowTokens(lexer.Tokens);
            _parseOut.Text = $"✗ LEXER ERROR\n\n  {lexer.Error}";
            SetStatus("✗  Lexer error", "#f8485e");
            return;
        }

        var parser = new Parser();
        var errors = parser.Parse(lexer.Tokens);
        ShowTokens(lexer.Tokens);
        ShowParseResult(errors);

        int count = 0;
        foreach (var t in lexer.Tokens)
            if (t.Type != TokenType.NewLine && t.Type != TokenType.Eof) count++;

        if (errors.Count == 0)
            SetStatus($"✓  Valid A++ program  ·  {count} tokens", "#34d399");
        else
            SetStatus($"✗  {errors.Count} parse error(s)", "#f8485e");
    }

    private void ShowTokens(List<Token> tokens)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var tok in tokens)
        {
            if (tok.Type == TokenType.NewLine || tok.Type == TokenType.Eof) continue;
            sb.AppendLine($" {tok.Line,3}:{tok.Col,-3}  {tok.Type,-18}  {tok.Value}");
        }
        _tokenOut.Text = sb.ToString();
    }

    private void ShowParseResult(List<string> errors)
    {
        if (errors.Count == 0)
        {
            _parseOut.Text =
                "✓  Program is syntactically valid.\n\n" +
                "   All function declarations, blocks,\n" +
                "   statements and expressions parsed OK.";
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✗  {errors.Count} error(s) found:\n");
            foreach (var e in errors) sb.AppendLine($"   • {e}");
            _parseOut.Text = sb.ToString();
        }
    }

    private void SetStatus(string msg, string hex)
    {
        _status.Text       = "  " + msg;
        _status.Foreground = new SolidColorBrush(Color.Parse(hex));
    }
}