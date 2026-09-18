public abstract class EstadoInimigoBase
{
    protected IAInimigoHibrida ia; 

    public EstadoInimigoBase(IAInimigoHibrida ia)
    {
        this.ia = ia;
    }

    public abstract void Entrar();     
    public abstract void Atualizar();  
    public abstract void Sair();     
}