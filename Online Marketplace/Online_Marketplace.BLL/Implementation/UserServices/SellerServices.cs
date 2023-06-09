﻿using Contracts;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Online_Marketplace.BLL.Extension;
using Online_Marketplace.BLL.Helpers;
using Online_Marketplace.BLL.Interface.IServices;
using Online_Marketplace.BLL.Interface.IUserServices;
using Online_Marketplace.DAL.Entities;
using Online_Marketplace.DAL.Entities.Models;
using Online_Marketplace.Logger.Logger;
using Online_Marketplace.Shared.DTOs;

namespace Online_Marketplace.BLL.Implementation.Services
{
    public sealed class SellerServices : ISellerServices
    {

        private readonly IRepository<Seller> _sellerRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerManager _logger;
        private readonly IUserServices _userServices;
        private readonly UserManager<User> _userManager;
        private readonly IAuthService _authService;

        private readonly IRepository<Wallet> _walletRepo;


        public SellerServices(IAuthService authService, ILoggerManager logger, IUnitOfWork unitOfWork, UserManager<User> userManager, IUserServices userServices)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _authService = authService; 
            _userServices = userServices;
            _sellerRepo = _unitOfWork.GetRepository<Seller>();
            _walletRepo = _unitOfWork.GetRepository<Wallet>();
        }


        public async Task<string> RegisterSeller(SellerForRegistrationDto sellerForRegistration)
        {
            _logger.LogInfo("Creating the Seller as a user first, before assigning the seller role to them and adding them to the Sellers table.");

            var user = await _userServices.RegisterUser(new UserForRegistrationDto
            {
                FirstName = sellerForRegistration.FirstName,
                LastName = sellerForRegistration.LastName,
                Email = sellerForRegistration.Email,
                Password = sellerForRegistration.Password,
                UserName = sellerForRegistration.UserName
            });

            await _userManager.AddToRoleAsync(user, "Seller");

            var seller = new Seller
            {
                FirstName = sellerForRegistration.FirstName,
                LastName = sellerForRegistration.LastName,
                PhoneNumber = sellerForRegistration.PhoneNumber,
                Email = sellerForRegistration.Email,
                BusinessName = sellerForRegistration.BusinessName,
                UserId = user.Id
            };

            await _sellerRepo.AddAsync(seller);
            await CreateCustomerAccount(seller);

            var verificationToken = Guid.NewGuid().ToString();
            var emailSent = await _authService.SendVerificationEmail(sellerForRegistration.Email, verificationToken);

            if (emailSent)
            {
               
                user.VerificationToken = verificationToken;
                await _userManager.UpdateAsync(user);

               
                var result = new { success = true, message = "Registration Successful! Please check your email for the verification link." };
                return JsonConvert.SerializeObject(result);
            }
            else
            {
               
                var result = new { success = false, message = "Failed to send verification email. Please try again later." };
                return JsonConvert.SerializeObject(result);
            }
        }



        private async Task CreateCustomerAccount(Seller seller )
        {
             Wallet wallet = new()
            {
                WalletNo = WalletIdGenerator.GenerateWalletId(),
                Balance = 0,
                IsActive = true,
                SellerId = seller.Id,   
            };
            await _walletRepo.AddAsync(wallet);
        }

       
    }
}
