public abstract class EstadoInimigoBase
{
    protected IAInimigo ia; 

    public EstadoInimigoBase(IAInimigo ia)
    {
        this.ia = ia;
    }

    public abstract void Entrar();     
    public abstract void Atualizar();  
    public abstract void Sair();     
}