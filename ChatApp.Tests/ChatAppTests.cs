using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ChatApp.Data;
using ChatApp.Models;
using ChatApp.Services;

namespace ChatApp.Tests
{
    public class ChatAppTests
    {
        private AppDbContext BuildContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public void TwoFactorService_GenerateAndValidateCode_Workflow()
        {
            var secret = TwoFactorService.GenerateSecret();
            Assert.NotNull(secret);

            var code = TwoFactorService.GenerateTotpCode(secret);
            Assert.NotNull(code);
            Assert.Equal(6, code.Length);

            var isValid = TwoFactorService.ValidateTotpCode(secret, code);
            Assert.True(isValid);
        }

        [Fact]
        public async Task SilentChannelMessage_IsStoredAsSilent()
        {
            await using var db = BuildContext();
            var user = new User { Username = "admin", PhoneNumber = "12345", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"), DisplayName = "Admin" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var channel = new Channel { Name = "news", CreatedById = user.Id, IsPublic = true };
            db.Channels.Add(channel);
            await db.SaveChangesAsync();

            var member = new ChannelMember { ChannelId = channel.Id, UserId = user.Id, IsAdmin = true };
            db.ChannelMembers.Add(member);
            await db.SaveChangesAsync();

            var message = new Message
            {
                SenderId = user.Id,
                ChannelId = channel.Id,
                Content = "Silent announcement",
                IsSilent = true,
                IsSent = true
            };
            db.Messages.Add(message);
            await db.SaveChangesAsync();

            var actual = await db.Messages.FirstOrDefaultAsync(m => m.ChannelId == channel.Id && m.IsSilent);
            Assert.NotNull(actual);
            Assert.True(actual.IsSilent);
            Assert.Equal("Silent announcement", actual.Content);
        }

        [Fact]
        public async Task PinUnpinFlow_CanPinAndUnpinMessage()
        {
            await using var db = BuildContext();
            var user = new User { Username = "u1", PhoneNumber = "111", PasswordHash = BCrypt.Net.BCrypt.HashPassword("p"), DisplayName = "U1" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var msg = new Message { SenderId = user.Id, ReceiverId = user.Id, Content = "hello", IsSent = true };
            db.Messages.Add(msg);
            await db.SaveChangesAsync();

            var pin = new PinnedMessage { MessageId = msg.Id, PinnedById = user.Id, DirectUserId = user.Id, Category = PinCategory.Important };
            db.PinnedMessages.Add(pin);
            await db.SaveChangesAsync();

            var loaded = await db.PinnedMessages.Include(p => p.Message).FirstOrDefaultAsync(p => p.MessageId == msg.Id);
            Assert.NotNull(loaded);
            Assert.Equal(PinCategory.Important, loaded.Category);

            db.PinnedMessages.Remove(loaded);
            await db.SaveChangesAsync();
            Assert.Null(await db.PinnedMessages.FirstOrDefaultAsync(p => p.MessageId == msg.Id));
        }

        [Fact]
        public async Task MessageReactions_AddAndToggleReactions()
        {
            await using var db = BuildContext();
            var u = new User { Username = "a", PhoneNumber = "555", PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"), DisplayName = "A" };
            db.Users.Add(u); await db.SaveChangesAsync();

            var msg = new Message { SenderId = u.Id, ReceiverId = u.Id, Content = "react", IsSent = true };
            db.Messages.Add(msg); await db.SaveChangesAsync();

            var r1 = new MessageReaction { MessageId = msg.Id, UserId = u.Id, Emoji = "👍" };
            db.MessageReactions.Add(r1); await db.SaveChangesAsync();

            var count = await db.MessageReactions.CountAsync(m => m.MessageId == msg.Id);
            Assert.Equal(1, count);

            // Toggle off
            db.MessageReactions.Remove(r1);
            await db.SaveChangesAsync();
            Assert.Equal(0, await db.MessageReactions.CountAsync(m => m.MessageId == msg.Id));
        }
    }
}
