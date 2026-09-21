using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BallMovement : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private float speed = 5f;

    [Header("Ricochete da Raquete")]
    [SerializeField] private float maxBounceAngle = 60f;

    [Header("Ricochete das Paredes")]
    [SerializeField] private float minimumVerticalAngle = 0.2f;

    private Rigidbody2D rb;

    private UDPClient udpClient;
    private bool possuiAutoridade;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        udpClient = FindFirstObjectByType<UDPClient>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.freezeRotation = true;

        rb.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        // A bola só começa a se mover depois que os dois jogadores entram.
        rb.linearVelocity = Vector2.zero;
        ConfigurarAutoridade(false);
    }

    public void ConfigurarAutoridade(bool autoridade)
    {
        possuiAutoridade = autoridade;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = autoridade
            ? RigidbodyType2D.Dynamic
            : RigidbodyType2D.Kinematic;
    }

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        // Somente Player 1 calcula
        // a física oficial da bola.
        if (!possuiAutoridade)
        {
            return;
        }

        if (collision.contactCount == 0)
            return;

        // =====================================================
        // COLISÃO COM RAQUETE
        // =====================================================

        if (collision.gameObject.CompareTag("Player"))
        {
            RicocheteRaquete(
                collision.transform
            );

            return;
        }

        // =====================================================
        // COLISÃO COM PAREDE
        // =====================================================

        Vector2 normal =
            collision.GetContact(0).normal;

        Vector2 velocidadeAtual =
            rb.linearVelocity.normalized;

        if (velocidadeAtual == Vector2.zero)
            return;

        Vector2 novaDirecao =
            Vector2.Reflect(
                velocidadeAtual,
                normal
            );

        // Evita que a bola fique praticamente
        // paralela à parede.
        if (Mathf.Abs(novaDirecao.y) <
            minimumVerticalAngle)
        {
            float sinal =
                novaDirecao.y >= 0f
                    ? 1f
                    : -1f;

            novaDirecao.y =
                minimumVerticalAngle *
                sinal;

            novaDirecao.Normalize();
        }

        rb.linearVelocity =
            novaDirecao * speed;
    }

    private void RicocheteRaquete(
        Transform raquete)
    {
        Collider2D colliderRaquete =
            raquete.GetComponent<Collider2D>();

        if (colliderRaquete == null)
            return;

        // Diferença entre a posição da bola
        // e o centro da raquete.
        float diferencaY =
            transform.position.y -
            raquete.position.y;

        // Metade da altura da raquete.
        float metadeAltura =
            colliderRaquete.bounds.extents.y;

        if (metadeAltura <= 0f)
            return;

        // Converte para -1 até 1.
        float percentual =
            diferencaY /
            metadeAltura;

        percentual =
            Mathf.Clamp(
                percentual,
                -1f,
                1f
            );

        // Calcula o ângulo do ricochete.
        float angulo =
            percentual *
            maxBounceAngle;

        float radianos =
            angulo *
            Mathf.Deg2Rad;

        // Descobre para qual lado
        // a bola deve ir.
        float direcaoX;

        if (transform.position.x <
            raquete.position.x)
        {
            direcaoX = -1f;
        }
        else
        {
            direcaoX = 1f;
        }

        // Calcula a nova direção.
        Vector2 novaDirecao =
            new Vector2(
                direcaoX *
                Mathf.Cos(radianos),

                Mathf.Sin(radianos)
            );

        novaDirecao.Normalize();

        // Aplica velocidade.
        rb.linearVelocity =
            novaDirecao * speed;
    }
}
