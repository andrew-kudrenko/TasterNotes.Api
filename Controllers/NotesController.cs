using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TasterNotes.Application.Services.Notes;
using TasterNotes.Persistence;
using TasterNotes.Extensions;
using TasterNotes.Utils;
using TasterNotes.Application.Mapping;
using TasterNotes.Application.Models.Request.Notes;

namespace TasterNotes.Controllers
{
    [Authorize]
    [Route("api/notes")]
    [ApiController]
    public class NotesController(AppDbContext db, NotesService notesService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> GetAllAsync() 
        {
            var userId = User.GetUserId();
            var notes = await db.Notes
                .AsNoTracking()
                .Where(n => n.UserId.Equals(userId))
                .ToListAsync();

            return Ok(notes.Select(NotesMapper.ToBriefNote));
        }

        [Route("{noteId}")]
        [HttpGet]
        public async Task<IActionResult> GetByIdAsync(Guid noteId)
        {
            var userId = User.GetUserId();

            var note = await db.Notes
                .AsNoTracking()
                .Include(n => n.Dishware)
                .Include(n => n.Taste)
                .Include(n => n.DescriptorSet)
                .SingleOrDefaultAsync(n => n.UserId.Equals(userId) && n.NoteId.Equals(noteId));

            if (note is null) 
            {
                return NotFound(new ErrorResponse("Not found"));
            }

            return Ok(NotesMapper.ToDetailedNote(note));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteRequest model) 
        {
            var note = await notesService.Create(model, User.GetUserId());

            return Ok(NotesMapper.ToDetailedNote(note));
        }

        [Route("{noteId}")]
        [HttpDelete]
        public async Task<IActionResult> Remove(Guid noteId)
        {
            var userId = User.GetUserId();
            var note = await db.Notes.SingleOrDefaultAsync(n => n.UserId.Equals(userId) && n.NoteId.Equals(noteId));

            if (note is null)
            {
                return NotFound(new ErrorResponse("Not found"));
            }

            db.Remove(note);
            await db.SaveChangesAsync();

            return Ok(note);
        }

        [Route("{noteId}")]
        [HttpPatch]
        public async Task<IActionResult> Update([FromBody] UpdateNoteRequest body, Guid noteId)
        {
            var userId = User.GetUserId();
            var note = await db.Notes
                .Include(n => n.Dishware)
                .Include(n => n.Taste)
                .Include(n => n.DescriptorSet)
                .SingleOrDefaultAsync(n => n.UserId.Equals(userId) && n.NoteId.Equals(noteId));

            if (note is null)
            {
                return NotFound(new ErrorResponse("Not found"));
            }

            body.Assign(note);

            db.Update(note);
            await db.SaveChangesAsync();

            return Ok(note);
        }
    }
}
