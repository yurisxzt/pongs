using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Pontuação")]
    [SerializeField] private TMP_Text scoreP1Text;
    [SerializeField] private TMP_Text scoreP2Text;

    [Header("Configuração da Partida")]
    [SerializeField] private int pontosParaVencer = 3;

    [Header("Bola")]
    [SerializeField] private Transform ball;
    [SerializeField] private Rigidbody2D ballRb;

    [Header("Tela de Vitória")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TMP_Text winText;

    [Header("Reinício")]
    [SerializeField] private float restartDelay = 1f;

    [Header("Rede")]
    [SerializeField] private UDPClient udpClient;

    private int scoreP1 = 0;
    private int scoreP2 = 0;

    private Vector3 ballStartPosition;

    private bool partidaEncerrada = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (ball != null)
        {
            ballStartPosition =
                ball.position;
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        AtualizarPlacar();

        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
        }
    }

    public void IniciarPartidaEmRede()
    {
        if (!SouPlayer1() || partidaEncerrada)
            return;

        ReiniciarBola();
    }

    public void AguardarOutroJogador()
    {
        CancelInvoke(nameof(LiberarBola));

        if (ball != null)
            ball.position = ballStartPosition;

        if (ballRb != null)
        {
            ballRb.linearVelocity = Vector2.zero;
            ballRb.angularVelocity = 0f;
        }
    }

    public void GolP1()
    {
        if (partidaEncerrada)
            return;

        // Somente Player 1 pode registrar
        // oficialmente um gol.
        if (!SouPlayer1())
            return;

        scoreP1++;

        AtualizarPlacar();

        EnviarPontuacao();

        if (scoreP1 >= pontosParaVencer)
        {
            FinalizarPartida(
                "Jogador 1 venceu!"
            );

            return;
        }

        ReiniciarBola();
    }

    public void GolP2()
    {
        if (partidaEncerrada)
            return;

        // Somente Player 1 pode registrar
        // oficialmente um gol.
        if (!SouPlayer1())
            return;

        scoreP2++;

        AtualizarPlacar();

        EnviarPontuacao();

        if (scoreP2 >= pontosParaVencer)
        {
            FinalizarPartida(
                "Jogador 2 venceu!"
            );

            return;
        }

        ReiniciarBola();
    }

    private bool SouPlayer1()
    {
        if (udpClient == null)
            return false;

        return udpClient.GetPlayerID() == 1;
    }

    private void AtualizarPlacar()
    {
        if (scoreP1Text != null)
        {
            scoreP1Text.text =
                scoreP1.ToString();
        }

        if (scoreP2Text != null)
        {
            scoreP2Text.text =
                scoreP2.ToString();
        }
    }

    private void EnviarPontuacao()
    {
        if (udpClient == null)
            return;

        udpClient.EnviarPontuacao(
            scoreP1,
            scoreP2
        );
    }

    public void SincronizarPontuacao(
        int novoScoreP1,
        int novoScoreP2)
    {
        scoreP1 = novoScoreP1;
        scoreP2 = novoScoreP2;

        AtualizarPlacar();

        if (scoreP1 >= pontosParaVencer)
        {
            FinalizarPartida(
                "Jogador 1 venceu!"
            );
        }
        else if (scoreP2 >= pontosParaVencer)
        {
            FinalizarPartida(
                "Jogador 2 venceu!"
            );
        }
    }

    private void ReiniciarBola()
    {
        if (ball == null)
            return;

        ball.position =
            ballStartPosition;

        if (ballRb != null)
        {
            ballRb.linearVelocity =
                Vector2.zero;

            ballRb.angularVelocity =
                0f;
        }

        CancelInvoke(
            nameof(LiberarBola)
        );

        Invoke(
            nameof(LiberarBola),
            restartDelay
        );
    }

    public void ReiniciarBolaRemota()
    {
        if (ball == null)
            return;

        ball.position =
            ballStartPosition;

        if (ballRb != null)
        {
            ballRb.linearVelocity =
                Vector2.zero;

            ballRb.angularVelocity =
                0f;
        }
    }

    private void LiberarBola()
    {
        if (partidaEncerrada)
            return;

        if (ballRb == null)
            return;

        // Somente Player 1 controla
        // fisicamente a bola.
        if (!SouPlayer1())
            return;

        float direcaoX =
            Random.value < 0.5f
                ? -1f
                : 1f;

        float direcaoY =
            Random.Range(
                -0.5f,
                0.5f
            );

        Vector2 direcao =
            new Vector2(
                direcaoX,
                direcaoY
            ).normalized;

        ballRb.linearVelocity =
            direcao * 5f;
    }

    private void FinalizarPartida(
        string mensagem)
    {
        partidaEncerrada = true;

        CancelInvoke(
            nameof(LiberarBola)
        );

        if (ballRb != null)
        {
            ballRb.linearVelocity =
                Vector2.zero;

            ballRb.angularVelocity =
                0f;
        }

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (winText != null)
        {
            winText.text =
                mensagem;
        }
    }

    public void ReiniciarPartida()
    {
        // Apenas Player 1 pode iniciar
        // oficialmente um novo jogo.
        if (!SouPlayer1())
            return;

        ReiniciarPartidaLocal();

        if (udpClient != null)
        {
            udpClient.EnviarPontuacao(
                scoreP1,
                scoreP2
            );

            udpClient.EnviarReinicio();
        }
    }

    public void ReiniciarPartidaRemota()
    {
        if (SouPlayer1())
            return;

        ReiniciarPartidaLocal();
    }

    private void ReiniciarPartidaLocal()
    {
        CancelInvoke(
            nameof(LiberarBola)
        );

        scoreP1 = 0;
        scoreP2 = 0;

        partidaEncerrada = false;

        AtualizarPlacar();

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (ball != null)
        {
            ball.position =
                ballStartPosition;
        }

        if (ballRb != null)
        {
            ballRb.linearVelocity =
                Vector2.zero;

            ballRb.angularVelocity =
                0f;
        }

        // Player 1 é quem lança a bola.
        if (SouPlayer1())
        {
            Invoke(
                nameof(LiberarBola),
                restartDelay
            );
        }
    }

    public int GetScoreP1()
    {
        return scoreP1;
    }

    public int GetScoreP2()
    {
        return scoreP2;
    }

    public bool PartidaEncerrada()
    {
        return partidaEncerrada;
    }
}
