using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPServer : MonoBehaviour
{
    [Header("Servidor")]
    [Tooltip("Deixe desmarcado para escolher Servidor ou Cliente ao abrir o jogo.")]
    [SerializeField] private bool executarServidor = false;
    [SerializeField] private int porta = 7777;

    private UdpClient servidor;
    private bool encerrando;

    private readonly Dictionary<string, int> jogadores =
        new Dictionary<string, int>();

    private readonly Dictionary<int, IPEndPoint> enderecosJogadores =
        new Dictionary<int, IPEndPoint>();

    private void Start()
    {
        if (executarServidor)
            IniciarServidor();
    }

    public bool IniciarServidor()
    {
        if (servidor != null)
            return true;

        try
        {
            encerrando = false;
            servidor = new UdpClient(new IPEndPoint(IPAddress.Any, porta));
            servidor.BeginReceive(ReceberMensagem, null);

            Debug.Log("SERVIDOR UDP iniciado na porta " + porta);
            return true;
        }
        catch (Exception erro)
        {
            servidor = null;
            Debug.LogError("Erro ao iniciar servidor UDP: " + erro.Message);
            return false;
        }
    }

    public bool EstaExecutando()
    {
        return servidor != null && !encerrando;
    }

    public void PararServidor()
    {
        FecharServidor();
    }

    private void ReceberMensagem(IAsyncResult resultado)
    {
        if (servidor == null || encerrando)
            return;

        try
        {
            IPEndPoint endereco = new IPEndPoint(IPAddress.Any, 0);
            byte[] dados = servidor.EndReceive(resultado, ref endereco);

            if (dados != null && dados.Length > 0)
            {
                string mensagem = Encoding.UTF8.GetString(dados);
                ProcessarMensagem(mensagem, endereco);
            }

            if (servidor != null && !encerrando)
                servidor.BeginReceive(ReceberMensagem, null);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException erro)
        {
            if (!encerrando)
                Debug.LogWarning("Erro de socket no servidor: " + erro.Message);
        }
        catch (Exception erro)
        {
            if (!encerrando)
                Debug.LogError("Erro ao receber mensagem: " + erro.Message);
        }
    }

    private void ProcessarMensagem(string mensagem, IPEndPoint endereco)
    {
        string identificador = endereco.ToString();

        if (mensagem.StartsWith("HELLO"))
        {
            int idSolicitado = ExtrairIDSolicitado(mensagem);
            RegistrarOuConfirmarJogador(identificador, endereco, idSolicitado);
            return;
        }

        if (!jogadores.TryGetValue(identificador, out int playerID))
        {
            EnviarMensagem("NOT_REGISTERED", endereco);
            return;
        }

        if (mensagem == "LEAVE")
        {
            RemoverJogador(identificador, playerID);
            return;
        }

        if (mensagem.StartsWith("POS:"))
        {
            EnviarParaOutroJogador(playerID, mensagem);
            return;
        }

        if (playerID != 1)
            return;

        if (mensagem.StartsWith("BALL:") ||
            mensagem.StartsWith("SCORE:") ||
            mensagem == "RESTART")
        {
            EnviarParaOutroJogador(playerID, mensagem);
        }
    }

    private int ExtrairIDSolicitado(string mensagem)
    {
        string[] partes = mensagem.Split(':');

        if (partes.Length == 2 &&
            int.TryParse(partes[1], out int id) &&
            (id == 1 || id == 2))
        {
            return id;
        }

        if (!enderecosJogadores.ContainsKey(1))
            return 1;

        if (!enderecosJogadores.ContainsKey(2))
            return 2;

        return 0;
    }

    private void RegistrarOuConfirmarJogador(
        string identificador,
        IPEndPoint endereco,
        int idSolicitado)
    {
        if (jogadores.TryGetValue(identificador, out int idExistente))
        {
            EnviarMensagem("ID:" + idExistente, endereco);
            AvisarSePartidaPronta();
            return;
        }

        if (idSolicitado == 0 || enderecosJogadores.ContainsKey(idSolicitado))
        {
            EnviarMensagem("FULL", endereco);
            return;
        }

        jogadores.Add(identificador, idSolicitado);
        enderecosJogadores.Add(idSolicitado, endereco);

        EnviarMensagem("ID:" + idSolicitado, endereco);
        Debug.Log("Jogador " + idSolicitado + " conectado em " + endereco);

        AvisarSePartidaPronta();
    }

    private void AvisarSePartidaPronta()
    {
        if (!enderecosJogadores.ContainsKey(1) ||
            !enderecosJogadores.ContainsKey(2))
        {
            return;
        }

        EnviarMensagem("READY", enderecosJogadores[1]);
        EnviarMensagem("READY", enderecosJogadores[2]);
    }

    private void RemoverJogador(string identificador, int playerID)
    {
        jogadores.Remove(identificador);
        enderecosJogadores.Remove(playerID);

        int outroID = playerID == 1 ? 2 : 1;
        if (enderecosJogadores.TryGetValue(outroID, out IPEndPoint outroEndereco))
            EnviarMensagem("WAIT", outroEndereco);
    }

    private void EnviarParaOutroJogador(int playerQueEnviou, string mensagem)
    {
        int outroJogador = playerQueEnviou == 1 ? 2 : 1;

        if (!enderecosJogadores.TryGetValue(
            outroJogador,
            out IPEndPoint enderecoDestino))
        {
            return;
        }

        EnviarMensagem(
            "P" + playerQueEnviou + ":" + mensagem,
            enderecoDestino
        );
    }

    private void EnviarMensagem(string mensagem, IPEndPoint endereco)
    {
        if (servidor == null || encerrando)
            return;

        try
        {
            byte[] dados = Encoding.UTF8.GetBytes(mensagem);
            servidor.Send(dados, dados.Length, endereco);
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

    private void OnDestroy()
    {
        FecharServidor();
    }

    private void OnApplicationQuit()
    {
        FecharServidor();
    }

    private void FecharServidor()
    {
        if (servidor == null)
            return;

        encerrando = true;
        servidor.Close();
        servidor = null;
        jogadores.Clear();
        enderecosJogadores.Clear();
    }
}
