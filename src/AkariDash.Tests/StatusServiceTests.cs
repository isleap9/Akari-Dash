using AkariDash.Framework.Services;
using Xunit;

namespace AkariDash.Tests;

public class StatusServiceTests
{
    [Fact]
    public void Shows_ready_until_a_job_begins_and_again_once_it_ends()
    {
        var status = new StatusService();
        Assert.Equal("Ready", status.Text);
        Assert.False(status.IsBusy);

        using (status.Begin("Uninstalling Microsoft Edge"))
        {
            Assert.Equal("Uninstalling Microsoft Edge", status.Text);
            Assert.True(status.IsBusy);
        }

        Assert.Equal("Ready", status.Text);
        Assert.False(status.IsBusy);
    }

    [Fact]
    public void The_newest_running_job_is_shown_and_the_older_one_returns_when_it_ends()
    {
        var status = new StatusService();
        var first = status.Begin("Removing News");
        var second = status.Begin("Restoring Clock");
        Assert.Equal("Restoring Clock", status.Text);

        second.Dispose();
        second.Dispose();
        Assert.Equal("Removing News", status.Text);

        first.Dispose();
        Assert.Equal("Ready", status.Text);
    }
}
