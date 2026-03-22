using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public abstract class BaseService
{
    protected readonly IUnitOfWork UnitOfWork;

    protected BaseService(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;
    }
}
