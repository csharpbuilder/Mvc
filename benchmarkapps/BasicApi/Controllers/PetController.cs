// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using BasicApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BasicApi.Controllers
{
    [Authorize("pet-store-reader")]
    [Route("/pet")]
    public class PetController : ControllerBase
    {
        private static bool _logged;

        public PetController(BasicApiContext dbContext)
        {
            DbContext = dbContext;
        }

        public BasicApiContext DbContext { get; }

        [HttpGet("{id}", Name = "FindPetById")]
        public async Task<IActionResult> FindById(int id)
        {
            var pet = await DbContext.Pets
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == id);

            return pet == null ? new NotFoundResult() : (IActionResult)new ObjectResult(pet);
        }

        [HttpGet("findByCategory/{categoryId}")]
        public async Task<IActionResult> FindByCategory(int categoryId)
        {
            var pet = await DbContext.Pets
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Category != null && p.Category.Id == categoryId);

            return pet == null ? new NotFoundResult() : (IActionResult)new JsonResult(pet);
        }

        [HttpGet("findByStatus")]
        public async Task<IActionResult> FindByStatus(string status)
        {
            var pet = await DbContext.Pets
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Status == status);

            return pet == null ? new NotFoundResult() : (IActionResult)new ObjectResult(pet);
        }

        [HttpGet("findByTags")]
        public async Task<IActionResult> FindByTags(string[] tags)
        {
            var pet = await DbContext.Pets
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Tags.Any(t => tags.Contains(t.Name)));

            return pet == null ? new NotFoundResult() : (IActionResult)new ObjectResult(pet);
        }

        [Authorize("pet-store-writer")]
        [HttpPost]
        public async Task<IActionResult> AddPet([FromBody] Pet pet)
        {
            if (!ModelState.IsValid)
            {
                return new BadRequestObjectResult(ModelState);
            }

            var originalId = pet.Id;
            try
            {
                DbContext.Pets.Add(pet);
                await DbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException)
            {
                // Temporarily, do not rethrow the Exception. This cleans up the server-side tracing.
                if (!_logged)
                {
                    _logged = true;

                    Console.WriteLine($"Pet id was {originalId} is {pet.Id}");
                    Console.WriteLine($"Exception ColumnName '{postgresException.ColumnName}'");
                    Console.WriteLine($"Exception ConstraintName '{postgresException.ConstraintName}'");
                    Console.WriteLine($"Exception Data.Count '{postgresException.Data.Count}'");
                    Console.WriteLine($"Exception DataTypeName '{postgresException.DataTypeName}'");
                    Console.WriteLine($"Exception Detail '{postgresException.Detail}'");
                    Console.WriteLine($"Exception ErrorCode '{postgresException.ErrorCode}'");
                    Console.WriteLine($"Exception HelpLink '{postgresException.HelpLink}'");
                    Console.WriteLine($"Exception Hint '{postgresException.Hint}'");
                    Console.WriteLine($"Exception HResult '{postgresException.HResult}'");
                    Console.WriteLine($"Exception InnerException '{postgresException.InnerException}'");
                    Console.WriteLine($"Exception InternalPosition '{postgresException.InternalPosition}'");
                    Console.WriteLine($"Exception InternalQuery '{postgresException.InternalQuery}'");
                    Console.WriteLine($"Exception Message '{postgresException.Message}'");
                    Console.WriteLine($"Exception MessageText '{postgresException.MessageText}'");
                    Console.WriteLine($"Exception SchemaName '{postgresException.SchemaName}'");
                    Console.WriteLine($"Exception Source '{postgresException.Source}'");
                    Console.WriteLine($"Exception SqlState '{postgresException.SqlState}'");
                    Console.WriteLine($"Exception Statement '{postgresException.Statement}'");
                    Console.WriteLine($"Exception TableName '{postgresException.TableName}'");
                    Console.WriteLine($"Exception Where '{postgresException.Where}'");

                    await Task.Delay(400);

                    var firstTag = await DbContext.Tags.FirstOrDefaultAsync();
                    var lastTag = await DbContext.Tags.LastOrDefaultAsync();
                    Console.WriteLine($"Tags have range {firstTag?.Id} to {lastTag?.Id}");

                    await Task.Delay(500);
                }
            }

            return new CreatedAtRouteResult("FindPetById", new { id = pet.Id }, pet);
        }

        [Authorize("pet-store-writer")]
        [HttpPut]
        public IActionResult EditPet(Pet pet)
        {
            throw new NotImplementedException();
        }

        [Authorize("pet-store-writer")]
        [HttpPost("{id}/uploadImage")]
        public IActionResult UploadImage(int id, IFormFile file)
        {
            throw new NotImplementedException();
        }

        [Authorize("pet-store-writer")]
        [HttpDelete("{id}")]
        public IActionResult DeletePet(int id)
        {
            throw new NotImplementedException();
        }
    }
}
