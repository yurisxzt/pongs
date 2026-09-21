using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

public class UDPClient : MonoBehaviour
{
    [Header("Conexão")]
    [SerializeField] private string ipServidor = "127.0.0.1";
    [SerializeField] private int porta = 7777;
    [SerializeField] private UDPServer udpServer;

    [Header("Raquetes")]
    [FormerlySerializedAs("minhaRaquete")]
    [SerializeField] private Transform player1;
    [FormerlySerializedAs("raqueteAdversaria")]
    [SerializeField] private Transform player2;

    [Header("Bola")]
    [SerializeField] private Transform bola;
    [SerializeField] private Rigidbody2D bolaRb;

    private UdpClient cliente;
    private IPEndPoint enderecoServidor;
    private Transform raqueteLocal;
    private Transform raqueteRemota;
    private Rigidbody2D raqueteRemotaRb;

    private int playerID;
    private int idSolicitado;
    private bool conectado;
    private bool encerrando;
    private bool partidaPronta;
    private bool modoEscolhido;
    private bool souServidor;

    private string ipDigitado;
    private string status = "Escolha como deseja jogar";
    private string ipLocal = "";

    private readonly Queue<string> mensagensRecebidas =
        new Queue<string>();
    private readonly object lockMensagens = new object();

    private float tempoEnvioPosicao;
    private const float IntervaloEnvio = 0.033f;
    private float tempoEnvioBola;
    private float tempoUltimoHello;

    private bool temPosicaoRaqueteRemota;
    private float alvoYRaqueteRemota;
    private bool temEstadoBolaRemota;
    private Vector2 alvoPosicaoBola;
    private Vector2 velocidadeBolaRemota;
    private float instanteEstadoBola;

    private void Awake()
    {
        ipDigitado = ipServidor;

        if (udpServer == null)
            udpServer = FindFirstObjectByType<UDPServer>();

        if (player1 == null)
        {
            GameObject objeto = GameObject.Find("Player1");
            if (objeto != null)
                player1 = objeto.transform;
        }

        if (player2 == null)
        {
            GameObject objeto = GameObject.Find("Player2");
            if (objeto != null)
                player2 = objeto.transform;
        }
    }

    private void Start()
    {
        ipLocal = ObterIPv4Local();
        ProcessarArgumentosDeLinhaDeComando();
    }

    private void Update()
    {
        ProcessarMensagensRecebidas();
        AplicarInterpolacaoRemota();

        if (!conectado || encerrando)
            return;

        if (playerID == 0)
        {
            tempoUltimoHello += Time.deltaTime;
            if (tempoUltimoHello >= 1f)
            {
                tempoUltimoHello = 0f;
                EnviarMensagem("HELLO:" + idSolicitado);
            }
            return;
        }

        if (raqueteLocal != null)
        {
            tempoEnvioPosicao += Time.deltaTime;
            if (tempoEnvioPosicao >= IntervaloEnvio)
            {
                tempoEnvioPosicao = 0f;
                EnviarPosicaoRaquete();
            }
        }

        if (playerID == 1 && partidaPronta && bola != null && bolaRb != null)
        {
            tempoEnvioBola += Time.deltaTime;
            if (tempoEnvioBola >= IntervaloEnvio)
            {
                tempoEnvioBola = 0f;
                EnviarEstadoBola();
            }
        }
    }

    private void OnGUI()
    {
        if (partidaPronta && playerID != 0)
            return;

        float largura = 420f;
        float altura = modoEscolhido ? 185f : 250f;
        Rect area = new Rect(
            (Screen.width - largura) * 0.5f,
            (Screen.height - altura) * 0.5f,
            largura,
            altura
        );

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Space(8f);
        GUILayout.Label("PONG EM REDE LOCAL", EstiloTitulo());
        GUILayout.Space(10f);

        if (!modoEscolhido)
        {
            GUILayout.Label("Servidor: joga com W / S");
            GUILayout.Label("Cliente: joga com as setas ↑ / ↓");
            if (status != "Escolha como deseja jogar")
                GUILayout.Label(status, EstiloCentralizado());
            GUILayout.Space(8f);

            if (GUILayout.Button("CRIAR SERVIDOR", GUILayout.Height(38f)))
                IniciarComoServidor();

            GUILayout.Space(8f);
            GUILayout.Label("IP do computador servidor:");
            ipDigitado = GUILayout.TextField(ipDigitado, 45);

            if (GUILayout.Button("ENTRAR COMO CLIENTE", GUILayout.Height(38f)))
                IniciarComoCliente(ipDigitado);
        }
        else
        {
            GUILayout.Label(status, EstiloCentralizado());

            if (souServidor)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Informe este IP ao cliente:", EstiloCentralizado());
                GUILayout.Label(ipLocal, EstiloIP());
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CANCELAR", GUILayout.Height(32f)))
                CancelarConexao();
        }

        GUILayout.EndArea();
    }

    private GUIStyle EstiloTitulo()
    {
        GUIStyle estilo = new GUIStyle(GUI.skin.label);
        estilo.alignment = TextAnchor.MiddleCenter;
        estilo.fontStyle = FontStyle.Bold;
        estilo.fontSize = 20;
        return estilo;
    }

    private GUIStyle EstiloCentralizado()
    {
        GUIStyle estilo = new GUIStyle(GUI.skin.label);
        estilo.alignment = TextAnchor.MiddleCenter;
        estilo.wordWrap = true;
        return estilo;
    }

    private GUIStyle EstiloIP()
    {
        GUIStyle estilo = EstiloCentralizado();
        estilo.fontStyle = FontStyle.Bold;
        estilo.fontSize = 22;
        return estilo;
    }

    public void IniciarComoServidor()
    {
        if (udpServer == null)
        {
            status = "O componente UDPServer não foi encontrado.";
            return;
        }

        if (!udpServer.IniciarServidor())
        {
            status = "Não foi possível abrir a porta UDP " + porta + ".";
            return;
        }

        souServidor = true;
        modoEscolhido = true;
        status = "Servidor criado. Aguardando o cliente...";
        Conectar("127.0.0.1", 1);
    }

    public void IniciarComoCliente(string enderecoIP)
    {
        enderecoIP = enderecoIP == null ? "" : enderecoIP.Trim();

        if (!IPAddress.TryParse(enderecoIP, out IPAddress endereco) ||
            endereco.AddressFamily != AddressFamily.InterNetwork)
        {
            status = "Digite um endereço IPv4 válido, por exemplo 192.168.0.10.";
            return;
        }

        souServidor = false;
        modoEscolhido = true;
        status = "Conectando ao servidor " + enderecoIP + "...";
        Conectar(enderecoIP, 2);
    }

    private void Conectar(string enderecoIP, int novoIDSolicitado)
    {
        FecharSocket(false);

        try
        {
            ipServidor = enderecoIP;
            idSolicitado = novoIDSolicitado;
            enderecoServidor = new IPEndPoint(IPAddress.Parse(ipServidor), porta);
            cliente = new UdpClient();
            cliente.Connect(enderecoServidor);

            conectado = true;
            encerrando = false;
            playerID = 0;
            partidaPronta = false;
            tempoUltimoHello = 0f;

            cliente.BeginReceive(ReceberMensagem, null);
            EnviarMensagem("HELLO:" + idSolicitado);
        }
        catch (Exception erro)
        {
            conectado = false;
            status = "Erro ao conectar: " + erro.Message;
            Debug.LogError(status);
        }
    }

    private void ProcessarArgumentosDeLinhaDeComando()
    {
        string[] argumentos = Environment.GetCommandLineArgs();

        foreach (string argumento in argumentos)
        {
            if (argumento.Equals("-host", StringComparison.OrdinalIgnoreCase))
            {
                IniciarComoServidor();
                return;
            }

            const string prefixo = "-serverIp=";
            if (argumento.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                IniciarComoCliente(argumento.Substring(prefixo.Length));
                return;
            }
        }
    }

    private void ConfigurarJogadorLocal()
    {
        if (playerID == 1)
        {
            raqueteLocal = player1;
            raqueteRemota = player2;
            status = "Servidor pronto. Aguardando o cliente...";
        }
        else
        {
            raqueteLocal = player2;
            raqueteRemota = player1;
            status = "Conectado. Aguardando o servidor iniciar...";
        }

        raqueteRemotaRb = raqueteRemota != null
            ? raqueteRemota.GetComponent<Rigidbody2D>()
            : null;

        BallMovement movimentoBola =
            bola != null ? bola.GetComponent<BallMovement>() : null;

        if (movimentoBola != null)
            movimentoBola.ConfigurarAutoridade(playerID == 1);
    }

    private void EnviarPosicaoRaquete()
    {
        string mensagem = "POS:" + raqueteLocal.position.y.ToString(
            "F3",
            CultureInfo.InvariantCulture
        );
        EnviarMensagem(mensagem);
    }

    private void EnviarEstadoBola()
    {
        Vector2 posicao = bola.position;
        Vector2 velocidade = bolaRb.linearVelocity;

        string mensagem = string.Join(
            ":",
            "BALL",
            posicao.x.ToString("F3", CultureInfo.InvariantCulture),
            posicao.y.ToString("F3", CultureInfo.InvariantCulture),
            velocidade.x.ToString("F3", CultureInfo.InvariantCulture),
            velocidade.y.ToString("F3", CultureInfo.InvariantCulture)
        );

        EnviarMensagem(mensagem);
    }

    public void EnviarPontuacao(int scoreP1, int scoreP2)
    {
        if (playerID == 1)
            EnviarMensagem("SCORE:" + scoreP1 + ":" + scoreP2);
    }

    public void EnviarReinicio()
    {
        if (playerID == 1)
            EnviarMensagem("RESTART");
    }

    private void EnviarMensagem(string mensagem)
    {
        if (cliente == null || encerrando)
            return;

        try
        {
            byte[] dados = Encoding.UTF8.GetBytes(mensagem);
            cliente.Send(dados, dados.Length);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException erro)
        {
            if (!encerrando)
                Debug.LogWarning("Falha ao enviar pacote UDP: " + erro.Message);
        }
    }

    private void ReceberMensagem(IAsyncResult resultado)
    {
        if (cliente == null || encerrando)
            return;

        try
        {
            IPEndPoint endereco = new IPEndPoint(IPAddress.Any, 0);
            byte[] dados = cliente.EndReceive(resultado, ref endereco);

            if (dados != null && dados.Length > 0)
            {
                string mensagem = Encoding.UTF8.GetString(dados);
                lock (lockMensagens)
                    mensagensRecebidas.Enqueue(mensagem);
            }

            if (cliente != null && conectado && !encerrando)
                cliente.BeginReceive(ReceberMensagem, null);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException erro)
        {
            if (!encerrando)
                Debug.LogWarning("Falha ao receber pacote UDP: " + erro.Message);
        }
    }

    private void ProcessarMensagensRecebidas()
    {
        while (true)
        {
            string mensagem;
            lock (lockMensagens)
            {
                if (mensagensRecebidas.Count == 0)
                    break;
                mensagem = mensagensRecebidas.Dequeue();
            }

            ProcessarMensagem(mensagem);
        }
    }

    private void ProcessarMensagem(string mensagem)
    {
        if (mensagem.StartsWith("ID:"))
        {
            if (int.TryParse(mensagem.Substring(3), out int novoID))
            {
                playerID = novoID;
                ConfigurarJogadorLocal();
                Debug.Log("Conectado como Jogador " + playerID);

                if (partidaPronta && GameManager.Instance != null)
                    GameManager.Instance.IniciarPartidaEmRede();
            }
            return;
        }

        if (mensagem == "READY")
        {
            bool jaEstavaPronta = partidaPronta;
            partidaPronta = true;
            if (playerID != 0)
            {
                status = playerID == 1
                    ? "Partida iniciada - use W / S"
                    : "Partida iniciada - use ↑ / ↓";
            }

            if (!jaEstavaPronta &&
                playerID != 0 &&
                GameManager.Instance != null)
            {
                GameManager.Instance.IniciarPartidaEmRede();
            }
            return;
        }

        if (mensagem == "WAIT")
        {
            partidaPronta = false;
            status = "O outro jogador saiu. Aguardando reconexão...";
            if (GameManager.Instance != null)
                GameManager.Instance.AguardarOutroJogador();
            return;
        }

        if (mensagem == "NOT_REGISTERED")
        {
            EnviarMensagem("HELLO:" + idSolicitado);
            return;
        }

        if (mensagem == "FULL")
        {
            status = "Essa função de jogador já está ocupada no servidor.";
            FecharSocket(false);
            return;
        }

        if (mensagem.StartsWith("P1:") || mensagem.StartsWith("P2:"))
        {
            int jogador = mensagem[1] - '0';
            ProcessarMensagemDoJogador(jogador, mensagem.Substring(3));
        }
    }

    private void ProcessarMensagemDoJogador(int jogador, string mensagem)
    {
        if (jogador == playerID)
            return;

        if (mensagem.StartsWith("POS:"))
        {
            if (float.TryParse(
                mensagem.Substring(4),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float posicaoY))
            {
                alvoYRaqueteRemota = posicaoY;
                temPosicaoRaqueteRemota = true;
            }
            return;
        }

        if (mensagem.StartsWith("BALL:") && playerID == 2)
        {
            string[] partes = mensagem.Substring(5).Split(':');
            if (partes.Length != 4)
                return;

            if (TentarLerFloat(partes[0], out float x) &&
                TentarLerFloat(partes[1], out float y) &&
                TentarLerFloat(partes[2], out float vx) &&
                TentarLerFloat(partes[3], out float vy))
            {
                alvoPosicaoBola = new Vector2(x, y);
                velocidadeBolaRemota = new Vector2(vx, vy);
                instanteEstadoBola = Time.time;
                temEstadoBolaRemota = true;
            }
            return;
        }

        if (mensagem.StartsWith("SCORE:"))
        {
            string[] partes = mensagem.Substring(6).Split(':');
            if (partes.Length == 2 &&
                int.TryParse(partes[0], out int scoreP1) &&
                int.TryParse(partes[1], out int scoreP2) &&
                GameManager.Instance != null)
            {
                GameManager.Instance.SincronizarPontuacao(scoreP1, scoreP2);
            }
            return;
        }

        if (mensagem == "RESTART" && playerID == 2 && GameManager.Instance != null)
            GameManager.Instance.ReiniciarPartidaRemota();
    }

    private bool TentarLerFloat(string valor, out float resultado)
    {
        return float.TryParse(
            valor,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out resultado
        );
    }

    private void AplicarInterpolacaoRemota()
    {
        float fator = 1f - Mathf.Exp(-20f * Time.deltaTime);

        if (temPosicaoRaqueteRemota && raqueteRemota != null)
        {
            float novoY = Mathf.Lerp(
                raqueteRemota.position.y,
                alvoYRaqueteRemota,
                fator
            );

            if (raqueteRemotaRb != null)
            {
                Vector2 posicao = raqueteRemotaRb.position;
                posicao.y = novoY;
                raqueteRemotaRb.position = posicao;
            }
            else
            {
                Vector3 posicao = raqueteRemota.position;
                posicao.y = novoY;
                raqueteRemota.position = posicao;
            }
        }

        if (playerID == 2 && temEstadoBolaRemota && bolaRb != null)
        {
            float atraso = Mathf.Min(Time.time - instanteEstadoBola, 0.08f);
            Vector2 posicaoPrevista = alvoPosicaoBola + velocidadeBolaRemota * atraso;
            bolaRb.position = Vector2.Lerp(bolaRb.position, posicaoPrevista, fator);
        }
    }

    private string ObterIPv4Local()
    {
        try
        {
            foreach (IPAddress endereco in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (endereco.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(endereco))
                {
                    return endereco.ToString();
                }
            }
        }
        catch (SocketException)
        {
        }

        return "Verifique com ipconfig";
    }

    private void CancelarConexao()
    {
        FecharSocket(true);

        if (souServidor && udpServer != null)
            udpServer.PararServidor();

        modoEscolhido = false;
        souServidor = false;
        status = "Escolha como deseja jogar";
    }

    public int GetPlayerID()
    {
        return playerID;
    }

    public bool EstaConectado()
    {
        return conectado && playerID != 0;
    }

    public bool PartidaPronta()
    {
        return partidaPronta;
    }

    private void OnDestroy()
    {
        FecharSocket(true);
    }

    private void OnApplicationQuit()
    {
        FecharSocket(true);
    }

    private void FecharSocket(bool avisarServidor)
    {
        if (cliente != null && avisarServidor && conectado && playerID != 0)
            EnviarMensagem("LEAVE");

        encerrando = true;
        conectado = false;
        partidaPronta = false;
        playerID = 0;
        raqueteLocal = null;
        raqueteRemota = null;
        raqueteRemotaRb = null;
        temPosicaoRaqueteRemota = false;
        temEstadoBolaRemota = false;

        if (cliente != null)
        {
            cliente.Close();
            cliente = null;
        }

        lock (lockMensagens)
            mensagensRecebidas.Clear();
    }
}
