using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbcRetail.Core.Models;
public class RegisterModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string CustomerId { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
    [StringLength(200)]
    public string? ShippingAddress { get; set; }
}
