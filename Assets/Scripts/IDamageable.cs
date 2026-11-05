// Всё, что может получить урон от лазера или дрона (астероид, босс).
// Благодаря этому Laser.cs не нужно знать, во что именно он попал.
public interface IDamageable
{
    void TakeDamage(int damage);
}
