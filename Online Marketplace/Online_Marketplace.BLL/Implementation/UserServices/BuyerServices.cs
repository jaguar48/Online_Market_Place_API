﻿using Contracts;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Online_Marketplace.BLL.Helpers;
using Online_Marketplace.BLL.Interface.IServices;
using Online_Marketplace.DAL.Entities.Models;
using Online_Marketplace.Logger.Logger;
using Online_Marketplace.Shared.DTOs;

namespace Online_Marketplace.BLL.Implementation.Services
{
    public class BuyerServices : IBuyerServices
    {
        private readonly IRepository<Buyer> _buyerRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerManager _logger;
        private readonly IUserServices _userServices;
        private readonly UserManager<User> _userManager;



        public BuyerServices(ILoggerManager logger, IUnitOfWork unitOfWork, UserManager<User> userManager, IUserServices userServices)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _userServices = userServices;
            _buyerRepo = _unitOfWork.GetRepository<Buyer>();

        }


        public async Task<string> RegisterBuyer(BuyerForRegistrationDto buyerForRegistration)
        {

            _logger.LogInfo("Creating the Buyer as a user first, before assigning the buyer role to them and them add them to Buyers table.");

            var user = await _userServices.RegisterUser(new UserForRegistrationDto
            {
                FirstName = buyerForRegistration.FirstName,
                LastName = buyerForRegistration.LastName,
                Email = buyerForRegistration.Email,
                Password = buyerForRegistration.Password,
                UserName = buyerForRegistration.UserName
            });

            await _userManager.AddToRoleAsync(user, "Buyer");

            var buyer = new Buyer
            {

                FirstName = buyerForRegistration.FirstName,
                LastName = buyerForRegistration.LastName,
                UserName = buyerForRegistration.UserName,
                PhoneNumber = buyerForRegistration.PhoneNumber,
                Email = buyerForRegistration.Email,
                Address = buyerForRegistration.Address,
                UserId = user.Id

            };

           await _buyerRepo.AddAsync(buyer);

            

            var result = new { success = true, message = "Registration Successful! You can now start buying products!" };
            return JsonConvert.SerializeObject(result);
        }
      
    }
}
