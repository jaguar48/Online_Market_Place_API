﻿using AutoMapper;
using Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Online_Marketplace.BLL.Extension;
using Online_Marketplace.BLL.Helpers;
using Online_Marketplace.BLL.Interface.IMarketServices;
using Online_Marketplace.DAL.Entities;
using Online_Marketplace.DAL.Entities.Models;
using Online_Marketplace.DAL.Enums;
using Online_Marketplace.Logger.Logger;
using Online_Marketplace.Shared.DTOs;
using PayStack.Net;
using System.Security.Claims;

namespace Online_Marketplace.BLL.Implementation.MarketServices
{
    public class OrderService : IOrderService
    {
        static IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IRepository<Product> _productRepo;
        private readonly IRepository<Buyer> _buyerRepo;
        private readonly IRepository<Seller> _sellerRepo;
        private readonly IRepository<Order> _orderRepo;
        private readonly IRepository<Cart> _cartRepo;
        private readonly IRepository<OrderItem> _orderitemRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerManager _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;



        public OrderService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILoggerManager logger, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _cartRepo = _unitOfWork.GetRepository<Cart>();
            _productRepo = _unitOfWork.GetRepository<Product>();
            _sellerRepo = _unitOfWork.GetRepository<Seller>();
            _buyerRepo = _unitOfWork.GetRepository<Buyer>();
            _orderRepo = _unitOfWork.GetRepository<Order>();
            _orderitemRepo = _unitOfWork.GetRepository<OrderItem>();
        }


        public async Task<List<OrderDto>> GetBuyerOrderHistoryAsync()
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var buyer = await _buyerRepo.GetSingleByAsync(b => b.UserId == userId);

            var orders = await _orderRepo.GetAllAsync(o => o.BuyerId == buyer.Id,
                include: o => o.Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Seller));

            var orderDtos = orders.Select(order =>
            {
                var orderDto = _mapper.Map<OrderDto>(order);
                orderDto.Total = order.OrderItems.Sum(oi => oi.Price * oi.Quantity);

                orderDto.OrderItems = order.OrderItems.Select(oi =>
                {
                    var orderItemDto = _mapper.Map<OrderItemDto>(oi);
                    orderItemDto.ProductName = oi.Product.Name;
                    orderItemDto.Total = oi.Price * oi.Quantity;
                    return orderItemDto;
                }).ToList();

                orderDto.SellerBusinessName = order.OrderItems.FirstOrDefault()?.Product?.Seller?.BusinessName;

                return orderDto;
            }).ToList();

            return orderDtos;
        }



        public async Task<List<OrderDto>> GetSellerOrderHistoryAsync()
        {
            var sellerId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var seller = await _sellerRepo.GetSingleByAsync(b => b.UserId == sellerId);
            var sellerProducts = await _productRepo.GetAllAsync(p => p.SellerId == seller.Id);
            var productIds = sellerProducts.Select(p => p.Id);
            var orderItems = await _orderitemRepo.GetAllAsync(
                oi => productIds.Contains(oi.ProductId),
                include: oi => oi.Include(oi => oi.Order).ThenInclude(o => o.Buyer));

            var orders = orderItems.GroupBy(oi => oi.OrderId).Select(group =>
            {
                var order = group.First().Order;

               
                var orderDto = _mapper.Map<OrderDto>(order);
                orderDto.Email = order.Buyer.Email;
                orderDto.Total = group.Sum(oi => oi.Price * oi.Quantity);
                orderDto.OrderItems = order.OrderItems.Select(o => new OrderItemDto
                {
                    ProductId = o.ProductId,
                    ProductName = o.Product.Name,
                    Price = o.Price,
                    Quantity = o.Quantity,
                    Total = o.Quantity * o.Price
                }).ToList();

                return orderDto;
            }).ToList();

            return orders;
        }

        public async Task<OrderDto> GetOrderByIdAsync(int orderId)
        {
            var sellerId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var seller = await _sellerRepo.GetSingleByAsync(b => b.UserId == sellerId);
            var sellerProducts = await _productRepo.GetAllAsync(p => p.SellerId == seller.Id);
            var productIds = sellerProducts.Select(p => p.Id);
            var orderItems = await _orderitemRepo.GetAllAsync(
                oi => productIds.Contains(oi.ProductId),
                include: oi => oi.Include(oi => oi.Order).ThenInclude(o => o.Buyer)
                                           .Include(oi => oi.Product));

            var order = orderItems.FirstOrDefault(oi => oi.OrderId == orderId)?.Order;

            if (order == null)
            {
                return null;
            }

            var orderDto = _mapper.Map<OrderDto>(order);
            orderDto.Email = order.Buyer.Email;
            orderDto.Total = order.OrderItems.Sum(oi => oi.Price * oi.Quantity);
            orderDto.OrderItems = order.OrderItems.Select(o => new OrderItemDto
            {
                ProductId = o.ProductId,
                ProductName = o.Product.Name,
                Price = o.Price,
                Quantity = o.Quantity,
                Total = o.Quantity * o.Price
            }).ToList();

            return orderDto;
        }



        public async Task<List<OrderStatusDto>> GetOrderStatusAsync(int orderId)
        {

            var buyerId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var buyer = await _buyerRepo.GetSingleByAsync(b => b.UserId == buyerId);

            var order = await _orderRepo.GetSingleByAsync(o => o.Id == orderId && o.BuyerId == buyer.Id,
                include: o => o.Include(oi => oi.OrderItems).ThenInclude(oi => oi.Product));

            if (order == null)
            {
                throw new Exception("Order not found");
            }

            var orderStatuses = order.OrderItems.Select(oi => new OrderStatusDto
            {
                ProductName = oi.Product.Name,
                Quantity = oi.Quantity,
                Price = oi.Price,
                Status = oi.Order.OrderStatus.ToString(),

            }).ToList();

            return orderStatuses;

        }



        public async Task<byte[]> GenerateReceiptAsync(int orderId)
        {

            var sellerid = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);


            var seller = await _sellerRepo.GetSingleByAsync(b => b.UserId == sellerid);


            if (seller == null)
            {
                throw new Exception("Seller not found");
            }

            var order = await _orderRepo.GetSingleByAsync(o => o.Id == orderId && o.OrderItems.Any(oi => oi.Product.SellerId == seller.Id),
                include: o => o.Include(oi => oi.OrderItems).ThenInclude(oi => oi.Product).Include(o => o.Buyer));

            if (order == null)
            {
                throw new Exception("Order not found");
            }

            var receipt = new ReceiptDto
            {
                OrderId = order.Id,
                OrderDate = order.OrderDate,
                BuyerName = order.Buyer.FirstName,
                BuyerEmail = order.Buyer.Email,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(oi => new ReceiptItemDto
                {
                    ProductName = oi.Product.Name,
                    Quantity = oi.Quantity,
                    Price = oi.Price
                }).ToList()
            };

            var receiptGenerator = new ReceiptGenerator();
            byte[] receiptBytes;
            using (var receiptStream = receiptGenerator.GenerateReceipt(receipt))
            {

                using (var memoryStream = new MemoryStream())
                {
                    await receiptStream.CopyToAsync(memoryStream);
                    receiptBytes = memoryStream.ToArray();
                }
            }

            return receiptBytes;
        }



        public async Task UpdateOrderStatusAsync(int OrderId, string Status )
        {

            var sellerId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var seller = await _sellerRepo.GetSingleByAsync(s => s.UserId == sellerId);

            var order = await _orderRepo.GetSingleByAsync(o => o.Id == OrderId && o.OrderItems.Any(oi => oi.Product.SellerId == seller.Id));

            if (order == null)
            {
                throw new Exception("Order not found");
            }

            var newStatus = Enum.Parse<OrderStatus>(Status);
            order.OrderStatus = newStatus;

            await _orderRepo.UpdateAsync(order);

        }


        public async Task<string> CheckoutAsync(int cartId, ShippingMethod shippingMethod)
        {
            var cart = await _cartRepo.GetSingleByAsync(
                c => c.Id == cartId,
                include: q => q.Include(c => c.CartItems).ThenInclude(ci => ci.Product)
            );

            if (cart == null)
            {
                throw new Exception("Cart not found");
            }

            if (cart.CartItems == null || !cart.CartItems.Any())
            {
                throw new Exception("Cart is empty");
            }

            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var buyer = await _buyerRepo.GetSingleByAsync(b => b.UserId == userId);

            if (buyer == null)
            {
                throw new Exception("Buyer not found");
            }

            var orderReference = OrderReferenceGenerator.GenerateOrderReference();

            var order = new Order
            {
                BuyerId = buyer.Id,
                Reference = orderReference,
                OrderDate = DateTime.UtcNow,
                OrderStatus = OrderStatus.Pending,
            };

            var (shippingCost, estimatedDeliveryDate) = await ShippingCalculator.CalculateShippingCostAsync(shippingMethod);
            order.ShippingCost = shippingCost;
            order.shippingmethod = shippingMethod.ToString();
            order.EstimateDeliveryDate = estimatedDeliveryDate;

            order.TotalAmount = cart.CartItems.Sum(ci => ci.Product.Price * ci.Quantity) + shippingCost;

            await _orderRepo.AddAsync(order);

            var orderItems = cart.CartItems.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity,
                Price = ci.Product.Price,
                OrderId = order.Id
            }).ToList();

            await _orderitemRepo.AddRangeAsync(orderItems);

            await _cartRepo.DeleteAsync(cart);

            _logger.LogInfo($"Checked out cart with ID {cart.Id}");

            var paymentRequest = new PaymentRequestDto
            {
                Amount = order.TotalAmount,
                Email = buyer.Email,
                Reference = order.Reference,
                CallbackUrl = "https://localhost:7258/marketplace/Products/verifypayment"
            };


            var transactionInitializeResponse = await MakePayment(paymentRequest);
           

            order.TransactionReference = transactionInitializeResponse.Data.Reference;
            order.PaymentGateway = "paystack";
            order.OrderStatus = OrderStatus.PendingPayment;

            await _orderRepo.UpdateAsync(order);

            _logger.LogInfo($"Payment initiated for order with ID {order.Id}");


            
            var authorizationUrl = transactionInitializeResponse.Data.AuthorizationUrl;
            var trimmedAuthorizationUrl = authorizationUrl.Split('?')[0]; 
            return trimmedAuthorizationUrl;


        }



        public async Task<TransactionInitializeResponse> MakePayment(PaymentRequestDto paymentRequestDto)
        {
            string secret = _configuration.GetSection("ApiSecret").GetSection("SecretKey").Value;

            var paystackApi = new PayStackApi(secret);

            var transactionInitializeRequest = new TransactionInitializeRequest
            {
                Email = paymentRequestDto.Email,
                AmountInKobo = (int)(paymentRequestDto.Amount * 100),
                Reference = paymentRequestDto.Reference,
                CallbackUrl = paymentRequestDto.CallbackUrl
            };

            var transactionInitializeResponse = paystackApi.Transactions.Initialize(transactionInitializeRequest);

            return transactionInitializeResponse;
        }


    }
}
