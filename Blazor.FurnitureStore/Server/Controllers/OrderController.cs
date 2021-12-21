using Blazor.FurnitureStore.Repositories;
using Blazor.FurnitureStore.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace Blazor.FurnitureStore.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderProductRepository _orderProductRepository;
        public OrderController(IOrderRepository orderRepository,
            IOrderProductRepository orderProductRepository)
        {
            _orderRepository = orderRepository;
            _orderProductRepository = orderProductRepository;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Order order)
        {
            if (order == null)
                return BadRequest();

            //validar propiedades especificas y mandar un error personalizado a la pantalla
            if (order.OrderNumber == 0)
                ModelState.AddModelError("OrderNumber", "Order number can't be empty");
            //mostrar un erro mas especifico que el de arriba
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using (TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                order.Id = await _orderRepository.GetNextId();//crear un id cnsecutivo antes de guardar
                await _orderRepository.InsertOrder(order);//insertar orden

                if (order.Products != null && order.Products.Any())
                {
                    foreach (var prd in order.Products)
                    {
                        await _orderProductRepository.InsertOrderProduct(order.Id, prd);//insertar id de la orden con productos
                    }
                }

                scope.Complete();
            }

            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Order order)
        {
            if (order == null)
                return BadRequest();

            if (order.OrderNumber == 0)
                ModelState.AddModelError("OrderNumber", "Order number can't be empty");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            //crear una transaccion real en sql server 
            using (TransactionScope scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {       
                //en lugar de llamar al insert llamas al update del repositorio actualizar el maestro
                await _orderRepository.UpdateOrder(order);

                //borramos todos los productos de la orden que ya venian y abajo los volveremos a insertar borramos todos los detalles
                await _orderProductRepository.DeleteOrderProductByOrder(order.Id);

                //recorrer la lista de productos en orden y guardarla en la tabla ordenproductos insertamos los detalles
                if (order.Products != null && order.Products.Any())
                {
                    foreach (var prd in order.Products)
                    {
                        await _orderProductRepository.InsertOrderProduct(order.Id, prd);
                    }
                }

                scope.Complete();
            }

            return NoContent();
        }


        [HttpGet("GetNextNumber")]
        public async Task<int> GetNextNumber()
        {
            return await _orderRepository.GetNextNumber();
        }

        [HttpGet]
        public async Task<IEnumerable<Order>> Get()
        {
            var orders = await _orderRepository.GetAll();

            foreach (var item in orders)
            {
                item.Products = (List<Product>)await _orderProductRepository.GetByOrder(item.Id);
            }

            return orders;
        }

        [HttpGet("{id}")]
        public async Task<Order> GetDetails(int id)
        {
            var order = await _orderRepository.GetDetails(id);
            var products = await _orderProductRepository.GetByOrder(id);

            if (order != null)
                order.Products = products.ToList();

            return order;
        }

        [HttpDelete("{id}")]
        public async Task Delete(int id)
        {
            await _orderRepository.DeleteOrder(id);
        }
    }
}
