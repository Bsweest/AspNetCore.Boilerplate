using System.Net;

namespace AspNetCore.Boilerplate.Domain;

public class EntityNotFoundException(string message) : DomainException(message)
{
    public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;
}
