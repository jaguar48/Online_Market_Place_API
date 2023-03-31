﻿using Online_Marketplace.DAL.Entities.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_Marketplace.DAL.Entities
{
    public class BuyerProfile : BaseUserProfile
    {
        public string Address { get; set; }
        public int WishlistId { get; set; }
        public int ShoppingCartId { get; set; }

        [ForeignKey(nameof(Buyer))]
        public int BuyerIdentity { get; set; }



        public Buyer Buyer { get; set; }
        public Wishlist Wishlist { get; set; }
        public ShoppingCart ShoppingCart { get; set; }
        public List<Order> Orders { get; set; } //orders buyer placed
    }
}