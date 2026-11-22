import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { BudgetBar } from './BudgetBar'

describe('BudgetBar', () => {
  it('is a meter with the numbers and the state in words', () => {
    render(<BudgetBar used={412} budget={1000} unit="µs" label="Time" />)
    const meter = screen.getByRole('meter', { name: 'Time' })
    expect(meter).toHaveAttribute('aria-valuenow', '412')
    expect(meter).toHaveAttribute('aria-valuemax', '1000')
    expect(meter).toHaveAttribute('aria-valuetext', '412 µs / 1 000 µs, within budget')
  })

  it('says when usage is over budget', () => {
    render(<BudgetBar used={4198} budget={1024} unit="B" label="Allocations" />)
    expect(screen.getByRole('meter', { name: 'Allocations' })).toHaveAttribute(
      'aria-valuetext',
      '4.1 KB / 1 KB, over budget',
    )
  })
})
