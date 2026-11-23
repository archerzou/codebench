import { describe, expect, it } from 'vitest'
import { formatValue, parameterNames } from './signature'

describe('parameterNames', () => {
  it.each([
    ['public long SumOfEvens(int[] nums)', ['nums']],
    ['public IList<string> TopK(string[] words, int k)', ['words', 'k']],
    ['public Task<List<string>> GetNames(ShopDb db, DateTime since)', ['db', 'since']],
    ['public int F(Dictionary<string, int> map, (int a, int b) pair = default)', ['map', 'pair']],
    ['public int NoArgs()', []],
    ['not a signature', []],
  ])('%s', (signature, expected) => {
    expect(parameterNames(signature)).toEqual(expected)
  })
})

describe('formatValue', () => {
  it('formats JSON compactly with spaces', () => {
    expect(formatValue([[1, 3], [2, 6]])).toBe('[[1, 3], [2, 6]]')
    expect(formatValue({ a: 1, b: 'x' })).toBe('{ "a": 1, "b": "x" }')
    expect(formatValue('banana')).toBe('"banana"')
    expect(formatValue(null)).toBe('null')
  })
})
