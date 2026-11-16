using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.Payments;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Features.Payments.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

/// <summary>
/// Payment endpoints (F7). Thin by design: bind input, call the Application
/// service, shape the HTTP result. Scope, transitions and validation live in
/// <see cref="IPaymentService"/> and the domain. Responses carry only billing
/// data and the patient's name; no medical data is exposed here.
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
[Authorize(Roles = Roles.Doctor + "," + Roles.Secretary)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    /// <summary>Records the payment of an appointment in the caller's scope.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await _payments.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
    }

    /// <summary>Gets one payment.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        await _payments.GetAsync(id, cancellationToken);

    /// <summary>Lists payments with optional filters and paging.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> List(
        [FromQuery] ListPaymentsQuery query,
        CancellationToken cancellationToken) =>
        await _payments.ListAsync(query, cancellationToken);

    /// <summary>
    /// Appointments in the caller's scope that can still be billed (not
    /// Cancelled or No-Show) and do not have a payment yet, for the payment
    /// form picker.
    /// </summary>
    [HttpGet("payable-appointments")]
    [ProducesResponseType(typeof(IReadOnlyList<PayableAppointmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PayableAppointmentResponse>>> GetPayableAppointments(
        [FromQuery] PayableAppointmentsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _payments.GetPayableAppointmentsAsync(query, cancellationToken));

    /// <summary>Updates the amount, method and notes of a payment that is not paid yet.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Update(
        Guid id,
        [FromBody] UpdatePaymentRequest request,
        CancellationToken cancellationToken) =>
        await _payments.UpdateAsync(id, request, cancellationToken);

    /// <summary>Moves a payment through Pending → Partial → Paid; Paid is final.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdatePaymentStatusRequest request,
        CancellationToken cancellationToken) =>
        await _payments.UpdateStatusAsync(id, request, cancellationToken);
}
