﻿using System.Security.Claims;
using CMS.API.Common.Mapping;
using CMS.API.Common.Message;
using CMS.API.Common.Validation;
using CMS.API.Entities;
using CMS.API.Exceptions;
using CMS.API.Infrastructure.Data;
using CMS.Shared.DTOs.AuClass.Response;
using CMS.Shared.DTOs.User.Request;
using CMS.Shared.DTOs.User.Response;
using KLPVN.Core.Helper;
using KLPVN.Core.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Services.User;

public class Services : IServices
{
  private readonly ApplicationDbContext _context;
  public Services(ApplicationDbContext dbContext)
  {
    _context = dbContext;
  }
  public async Task<Guid> CreateAsync(CreateUserRequest request)
  {
    request.IsValid();
    var requestUerName = await _context.Users.FirstOrDefaultAsync(x => x.UserName == request.UserName);
    if (requestUerName is not null)
    {
      throw new BadRequestException(ConstMessage.USER_NAME_NOT_EXITS);
    }
    var user = request.Mapping();
    user.Id = user.Id;
    user.IsActive = true;
    user.Salt = SecurityHelper.GenerateSalt();
    user.PasswordHash = SecurityHelper.HashPassword(request.Password,user.Salt);
    _context.Users.Add(user);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task<string> ResetPasswordAsync(string userName)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }
    var passwordRest = StringHelper.GeneratorRandomPassword();
    user.Salt = SecurityHelper.GenerateSalt();
    user.PasswordHash = SecurityHelper.HashPassword(passwordRest, user.Salt);
    _context.Users.Update(user);
    await _context.SaveChangesAsync();
    return passwordRest;
  }

  public async Task<Guid> ChangePasswordAsync(string userName, ChangePasswordRequest request)
  {
    request.IsValid();
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }

    if (!SecurityHelper.VerifyPassword(request.Password, user.Salt, user.PasswordHash))
    {
      throw new BadRequestException(ConstMessage.PASSORD_NOT_CORRECT);
    }
    
    user.Salt = SecurityHelper.GenerateSalt();
    user.PasswordHash = SecurityHelper.HashPassword(request.NewPassword, user.Salt);
    _context.Users.Update(user);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task<Entities.User> ActiveUserAsync(string userName)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }
    user.IsActive = !user.IsActive;
    _context.Users.Update(user);
    await _context.SaveChangesAsync();
    return user;
  }

  public async Task<Guid> UpdateAsync(string userName, UpdateUserInformationRequest request)
  {
    request.IsValid();
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }
    request.Mapping(user);
    _context.Users.Update(user);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task<Guid> AddRoleForUserAsync(string userName, Guid roleId)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }

    var role = await _context.Roles.FirstOrDefaultAsync(x => x.Id == roleId);
    if (role is null)
    {
      throw new NotFoundException(nameof(role));
    }
    var userRoleExits = await _context.UserRoles.FirstOrDefaultAsync(x=>x.UserId == user.Id && x.RoleId == role.Id);
    if (userRoleExits is not null)
    {
      throw new BadRequestException(ConstMessage.USER_HAS_ROLE);
    }
    var userRole = new Entities.UserRole(user.Id, role.Id);
    _context.UserRoles.Add(userRole);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task<Guid> RemoveRoleForUserAsync(string userName, Guid roleId)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }
    var userRole = await _context.UserRoles.FirstOrDefaultAsync(x=>x.RoleId == roleId && x.UserId == user.Id);
    if (userRole is null)
    {
      throw new NotFoundException("Role for user");
    }
    _context.UserRoles.Remove(userRole);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task DeleteAsync(string userName)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }

    _context.Users.Remove(user);
    await _context.SaveChangesAsync();
  }
  public async Task<MenuTreeResponse> GetMenuTreeForUserAsync(IEnumerable<string>? permissionCode)
  {
    // have permission code => return permission id 
    //=>  step 1: au action class for permission id
    // get parent menu or in top level
    // please logout after add role or permission
    //
    var menuResponses = new MenuTreeResponse();
    Dictionary<string, MenuResponse> menuMemo = [];
    permissionCode ??= [];
    var auAction = await _context.AuActionClasses
      .Where(x => permissionCode.Contains(x.Code))
      .ToListAsync();
    foreach (var a  in auAction)
    {
      var auClass = await _context.AuClasses
        .FirstOrDefaultAsync(x => x.Id == a.ClassId);
      var menuBottom = new MenuResponse()
      {
        NameAction = a.Name,
        Path = a.Path
      };
      await GetTreeMenuAsync(auClass, menuBottom, menuResponses, menuMemo);
    }
    
    return menuResponses;
  }

  public async Task<UserDetailResponse> GetUserDetailsAsync(string userName)
  {
    var user = await _context.Users.Where(x => x.UserName == userName).FirstOrDefaultAsync();
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }

    var userResponse = user.Mapping();
    return userResponse;
  }

  public async Task<Guid> UploadAvatarAsync(string userName, string avatarUrl)
  {
    var user = await _context.Users.FirstOrDefaultAsync(x => x.UserName == userName);
    if (user is null)
    {
      throw new NotFoundException(nameof(user));
    }
    user.Avatar = avatarUrl;
    _context.Users.Update(user);
    await _context.SaveChangesAsync();
    return user.Id;
  }

  public async Task<List<UserDescriptionResponse>> GetListUserDescriptionsAsync()
  {
    var users = await _context.Users
      .Include(x=>x.UserRoles)
      .ThenInclude(x=>x.Role)
      .OrderBy(x=>x.CreatedDatetime)
      .AsNoTracking()
      .ToListAsync();
    var userResponse = users.Mapping();
    return userResponse;
  }

  private async Task GetTreeMenuAsync(Entities.AuClass? auClass, MenuResponse menuBottom
    , MenuTreeResponse menuResponses, Dictionary<string, MenuResponse> menusMemo)
  {
    if (auClass is null)
    {
      menuResponses.Menu.Add(menuBottom);
      return ;
    }

    if (menusMemo.ContainsKey(auClass.Code))
    {
      menusMemo[auClass.Code].MenuChild.Add(menuBottom);
      return;
    }
    
    var menuNew = new MenuResponse()
    {
      MenuCode = auClass.Code,
      MenuName = auClass.MenuName,
      MenuChild = [menuBottom]
    };
    
    menusMemo.Add(auClass.Code, menuNew);
    var parentMenu = await _context.AuClasses
      .FirstOrDefaultAsync(x => x.Id == auClass.ParentId);
    await GetTreeMenuAsync(parentMenu,menuNew, menuResponses, menusMemo);
  }
  
}
