﻿using CMS.API.Common.Mapping;
using CMS.API.Common.Message;
using CMS.API.Common.Validation;
using CMS.API.Entities;
using CMS.API.Exceptions;
using CMS.API.Infrastructure.Data;
using CMS.Shared.DTOs.Content.Request;
using KLPVN.Core.Interface;
using Microsoft.EntityFrameworkCore;

namespace CMS.API.Services.Content;

public class Services : IServices
{
  private readonly ApplicationDbContext _context;

  public Services(ApplicationDbContext dbContext)
  {
    _context = dbContext;
  }

  public async Task<Guid> CreateAsync(CreateContentRequest request)
  {
    request.IsValid();

    var subjects = await _context.Subjects
      .Where(x => x.IsActive && request.SubjectId.Contains(x.Id))
      .CountAsync();
    if (request.SubjectId.Count() != subjects)
    {
      throw new BadRequestException(ConstMessage.SUBJECT_IN_VALIDATION_OR_NOT_HAS_ACTIVE_BEFORE);
    }
    var content = request.Mapping();
    try
    {
      await _context.Database.BeginTransactionAsync();
      _context.Contents.Add(content);
      var subjectContents = request.SubjectId.Select(x => new SubjectContent(x, content.Id));
      _context.SubjectContents.AddRange(subjectContents);
      await _context.SaveChangesAsync();
      await _context.Database.CommitTransactionAsync();
      return content.Id;
    }
    catch (Exception ex)
    {
      await _context.Database.RollbackTransactionAsync();
      throw new ErrorProcessing();
    }
  }

  public async Task<Guid> UpdateAsync(Guid id, UpdateContentRequest request)
  {
    request.IsValid();
    var content = await _context.Contents.FirstOrDefaultAsync(x=>x.Id == id);
    if (content is null)
    {
      throw new NotFoundException(nameof(content));
    }
    var subjects = await _context.Subjects
      .Where(x => x.IsActive && request.SubjectId.Contains(x.Id))
      .CountAsync();
    if (request.SubjectId.Count() != subjects)
    {
      throw new BadRequestException(ConstMessage.SUBJECT_IN_VALIDATION_OR_NOT_HAS_ACTIVE_BEFORE);
    }
    request.Mapping(content);
    try
    {
      await _context.Database.BeginTransactionAsync();
      _context.Contents.Update(content);
      await _context.SubjectContents.Where(x => x.ContentId == id).ExecuteDeleteAsync();
      var subjectContents = request.SubjectId.Select(x=>new SubjectContent(x, content.Id));
      _context.SubjectContents.AddRange(subjectContents);
      await _context.SaveChangesAsync();
      await _context.Database.CommitTransactionAsync();
      return content.Id;
    }
    catch (Exception ex)
    {
      await _context.Database.RollbackTransactionAsync();
      throw new ErrorProcessing();
    }
  }

  public async Task DeleteAsync(Guid id)
  {
    var content = await _context.Contents.FirstOrDefaultAsync(x => x.Id == id);
    if (content is null)
    {
      throw new NotFoundException(nameof(content));
    }
    _context.Contents.Remove(content);
    await _context.SaveChangesAsync();
  }

  public async Task<Guid> ActiveAsync(Guid id)
  {
    var content = await _context.Contents.FirstOrDefaultAsync(x => x.Id == id);
    if (content is null)
    {
      throw new NotFoundException(nameof(content));
    }
    content.IsActive = !content.IsActive;
    _context.Contents.Update(content);
    await _context.SaveChangesAsync();
    return content.Id;
  }
}
