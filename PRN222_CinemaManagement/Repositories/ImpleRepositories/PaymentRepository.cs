using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;

namespace PRN222_CinemaManagement.Repositories.ImpleRepositories;

public class PaymentRepository(CinemaDbContext context)
    : GenericRepository<Payment>(context), IPaymentRepository
{
}

